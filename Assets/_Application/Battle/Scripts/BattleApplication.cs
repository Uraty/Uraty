using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using R3;

using UnityEngine;
using UnityEngine.SceneManagement;

using Uraty.Features.Bot;
using Uraty.Features.Character;
using Uraty.Features.Player;
using Uraty.Features.Timer;
using Uraty.Shared.Team;
using Uraty.Shared.Role;
using Uraty.Shared.Entry;
using Uraty.Systems.Camera;
using Uraty.Systems.Input;

namespace Uraty.Application.Battle
{
    /// <summary>
    /// バトルシーン全体の生成、入力接続、Bot制御、可視性制御、復活処理、バトル時間を管理するアプリケーション層のコンポーネントです。
    /// </summary>
    public sealed class BattleApplication : MonoBehaviour
    {
        /// <summary>
        /// 1チームあたりの生成人数です。
        /// </summary>
        private const int TeamMemberCount = 3;

        /// <summary>
        /// 方向ベクトルを有効とみなす最小二乗長です。
        /// </summary>
        private const float MinDirectionSqrMagnitude = 0.0001f;

        /// <summary>
        /// Terrain asmdef への直接参照を避けるために Reflection で解決する Spawner の完全修飾名です。
        /// </summary>
        private const string SpawnerTypeName =
            "Uraty.Features.Terrain.Spawner";

        private static Type _cachedSpawnerType;
        private static PropertyInfo _cachedSpawnerTeamIdProperty;
        private static MethodInfo _cachedSpawnerTryReserveMethod;
        private static bool _isSpawnerReflectionCached;

        [Header("Battle Timer")]
        [SerializeField]
        private float _battleDurationSeconds = 180f;

        [SerializeField]
        private CountDown _countDown;

        [Header("Auto Aim (Player)")]
        [SerializeField]
        private float _playerAutoAimSearchRadius = 12f;

        [SerializeField]
        private float _playerAutoAimMaxAngleDegrees = 55f;

        [Header("Bot Recovery")]
        [Tooltip("Botが逃走・回復に入るHP比率(0-1)")]
        [SerializeField]
        private float _botRecoveryEnterHpRatio = 0.5f;

        [Tooltip("回復開始後、このHP比率まで回復したら通常行動へ戻る(0-1)")]
        [SerializeField]
        private float _botRecoveryExitHpRatio = 0.7f;

        [Tooltip("逃走移動の強さ(0-1)")]
        [SerializeField]
        private float _botFleeMoveScale = 1.0f;

        [Header("Respawn")]
        [SerializeField]
        private float _respawnDelaySeconds = 3f;

        [Header("Camera")]
        [SerializeField]
        private Camera _playerCamera;

        [Header("Input")]
        [SerializeField]
        private GameInput _input;

        [SerializeField]
        private PlayerController _playerController;

        [Header("Bot")]
        [SerializeField]
        private BotController[] _botControllers;

        [Header("Visibility")]
        [SerializeField]
        private TeamId _visibleTeamId = TeamId.Primary;

        [Header("Fallback")]
        [SerializeField]
        private RoleType _fallbackPlayerRoleType = RoleType.Attacker;

        [Header("Character Prefabs")]
        [SerializeField]
        private RoleCharacterPrefabEntry[] _roleCharacterPrefabEntries;

        [Header("Spawn")]
        [Tooltip("スポナーを検索する対象レイヤー")]
        [SerializeField]
        private LayerMask _spawnerLayerMask;

        [Header("ブッシュ内出た間を発射したときの見える時間")]
        [SerializeField]
        private float _attackRevealDuration = 1.0f;

        [Header("Battle Scene Entry")]
        [SerializeField]
        private BattleSceneEntry _battleSceneEntry;

        private readonly Dictionary<GameObject, float>
            _temporaryRevealEndTimeByCharacterObject = new();

        private bool _hasRequestedResultScene;

        private float _remainingBattleSeconds;
        private bool _isBattleTimerRunning;

        public int CharacterCount => _characterEntries.Count;

        public bool IsBattleTimerRunning => _isBattleTimerRunning;

        public float RemainingBattleSeconds => _remainingBattleSeconds;

        public bool TryGetCharacterStatusAt(
            int index,
            out CharacterStatus status)
        {
            status = null;

            if (index < 0
                || index >= _characterEntries.Count)
            {
                return false;
            }

            CharacterRuntimeEntry entry =
                _characterEntries[index];

            if (entry == null
                || entry.Status == null)
            {
                return false;
            }

            status =
                entry.Status;

            return true;
        }

        private readonly List<CharacterRuntimeEntry> _characterEntries = new();

        private readonly Dictionary<GameObject, CharacterRuntimeEntry>
            _characterEntryByObject = new();

        private readonly Dictionary<GameObject, bool>
            _isBotRecoveringByCharacterObject = new();

        private readonly Dictionary<GameObject, Component>
            _spawnerByCharacterObject = new();

        private DisposableBag _disposables;

        private void Awake()
        {
            _remainingBattleSeconds =
                Mathf.Max(
                    0f,
                    _battleDurationSeconds);

            _isBattleTimerRunning =
                false;

            _hasRequestedResultScene =
                false;
        }

        private IEnumerator Start()
        {
            yield return null;

            ResetBattleTimer();

            _input.Player.Enable();

            GameObject playerObject;

            if (!TrySpawnCharactersFromBattleSceneEntry(out playerObject))
            {
                Debug.LogWarning(
                    $"{nameof(BattleApplication)}: BattleSceneEntryから生成できなかったため、Fallback生成を使います。");

                RoleType[] roleTypes =
                    (RoleType[])Enum.GetValues(
                        typeof(RoleType));

                int selectedIndex =
                    Array.IndexOf(
                        roleTypes,
                        _fallbackPlayerRoleType);

                if (selectedIndex < 0)
                {
                    selectedIndex = 0;
                }

                playerObject =
                    SpawnPlayerTeam(
                        roleTypes,
                        selectedIndex);

                SpawnEnemyTeam(
                    roleTypes,
                    selectedIndex);
            }

            UpdateReloadBarVisibility(
                playerObject);

            ConfigureBushRevealSensors(
                _visibleTeamId);

            _playerCamera
                .GetComponent<CameraMove>()
                .SetTarget(playerObject);

            SubscribePlayerController(
                playerObject);

            SubscribeBotControllers(
                playerObject);

            StartBattleTimer();
        }

        private void Update()
        {
            UpdateBattleTimer(
                Time.deltaTime);

            UpdateCharacterVisibility();
        }

        public void StartBattleTimer()
        {
            _isBattleTimerRunning =
                true;

            UpdateBattleTimerDisplay();
        }

        public void StopBattleTimer()
        {
            _isBattleTimerRunning =
                false;
        }

        public void ResetBattleTimer()
        {
            _remainingBattleSeconds =
                Mathf.Max(
                    0f,
                    _battleDurationSeconds);

            UpdateBattleTimerDisplay();
        }

        private void UpdateBattleTimer(
            float deltaTime)
        {
            if (!_isBattleTimerRunning)
            {
                return;
            }

            if (_remainingBattleSeconds > 0f)
            {
                _remainingBattleSeconds =
                    Mathf.Max(
                        0f,
                        _remainingBattleSeconds - deltaTime);

                UpdateBattleTimerDisplay();

                if (_remainingBattleSeconds > 0f)
                {
                    return;
                }
            }

            _isBattleTimerRunning =
                false;

            HandleBattleTimerCompleted();
        }

        private void UpdateBattleTimerDisplay()
        {
            if (_countDown == null)
            {
                return;
            }

            _countDown.SetRemainingSeconds(
                _remainingBattleSeconds);
        }

        private void HandleBattleTimerCompleted()
        {
            if (_hasRequestedResultScene)
            {
                return;
            }

            _hasRequestedResultScene =
                true;

            StopBattleTimer();

            if (_input != null)
            {
                _input.Player.Disable();
            }

            Debug.Log(
                "Battle timer completed. Change scene to Result.");

            SceneManager.LoadScene("ResultScene");
        }

        private bool TrySpawnCharactersFromBattleSceneEntry(
    out GameObject playerObject)
        {
            playerObject =
                null;

            if (_battleSceneEntry == null)
            {
                Debug.LogError(
                    $"{nameof(BattleApplication)}: BattleSceneEntry が設定されていません。");

                return false;
            }

            if (!_battleSceneEntry.TryConsume(
                    out TeamId[] teamIds,
                    out RoleType[] roleTypes))
            {
                Debug.LogError(
                    $"{nameof(BattleApplication)}: BattleSceneEntry に情報がありません。");

                return false;
            }

            if (teamIds.Length != BattleSceneEntry.CharacterCount
                || roleTypes.Length != BattleSceneEntry.CharacterCount)
            {
                Debug.LogError(
                    $"{nameof(BattleApplication)}: BattleSceneEntry の配列数が不正です。");

                return false;
            }

            for (int i = 0; i < BattleSceneEntry.CharacterCount; i++)
            {
                if (!HasCharacterPrefab(roleTypes[i]))
                {
                    Debug.LogError(
                        $"{nameof(BattleApplication)}: {roleTypes[i]} のPrefabが未登録です。");

                    return false;
                }
            }

            for (int i = 0; i < BattleSceneEntry.CharacterCount; i++)
            {
                GameObject characterObject =
                    SpawnCharacter(
                        roleTypes[i],
                        teamIds[i]);

                if (i == BattleSceneEntry.PlayerIndex)
                {
                    playerObject =
                        characterObject;

                    _visibleTeamId =
                        teamIds[i];
                }
            }

            if (playerObject == null)
            {
                Debug.LogError(
                    $"{nameof(BattleApplication)}: Player用Characterが生成されませんでした。");

                return false;
            }

            return true;
        }

        private GameObject SpawnPlayerTeam(
            RoleType[] roleTypes,
            int selectedIndex)
        {
            GameObject playerObject = null;

            for (int i = 0;
                 i < TeamMemberCount;
                 i++)
            {
                RoleType roleType =
                    roleTypes[
                        (selectedIndex + i)
                        % roleTypes.Length];

                GameObject characterObject =
                    SpawnCharacter(
                        roleType,
                        TeamId.Primary);

                if (i == 0)
                {
                    playerObject =
                        characterObject;
                }
            }

            if (playerObject == null)
            {
                throw new InvalidOperationException(
                    "操作対象 Character が生成されませんでした。");
            }

            return playerObject;
        }

        private void SpawnEnemyTeam(
            RoleType[] roleTypes,
            int selectedIndex)
        {
            for (int i = 0;
                 i < TeamMemberCount;
                 i++)
            {
                RoleType roleType =
                    roleTypes[
                        (selectedIndex
                         + TeamMemberCount
                         + i)
                        % roleTypes.Length];

                SpawnCharacter(
                    roleType,
                    TeamId.Secondary);
            }
        }

        private GameObject SpawnCharacter(
            RoleType roleType,
            TeamId teamId)
        {
            GameObject prefab =
                FindCharacterPrefab(roleType);

            GameObject obj =
                Instantiate(prefab);

            // チームカラー設定
            Color teamColor =
                teamId == TeamId.Primary
                    ? new Color(255f / 255f, 85f / 255f, 125f / 255f, 0.7f)     // 味方
                    : new Color(185f / 255f, 225f / 255f, 95f / 255f, 0.7f);     // 敵
            
            Renderer[] renderers =
                obj.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                renderer.material.color = teamColor;
            }

            Component spawner =
                AssignCharacterToSpawnerPosition(
                    obj,
                    teamId);

            _spawnerByCharacterObject[obj] =
                spawner;

            CharacterStatus status =
                GetRequiredComponent<CharacterStatus>(
                    obj);

            status.Initialize(teamId);

            CharacterRuntimeEntry entry =
                CreateCharacterRuntimeEntry(obj);

            _characterEntries.Add(entry);

            _characterEntryByObject[obj] =
                entry;

            SubscribeCharacterDeath(entry);

            return obj;
        }

        private CharacterRuntimeEntry CreateCharacterRuntimeEntry(
            GameObject characterObject)
        {
            return new CharacterRuntimeEntry(
                characterObject,
                characterObject.transform,
                GetRequiredComponent<CharacterStatus>(
                    characterObject),
                GetRequiredComponent<CharacterReveal>(
                    characterObject),
                characterObject
                    .GetComponentInChildren<HPSystem>(
                        true),
                characterObject
                    .GetComponentInChildren<CharacterReloadBar>(
                        true),
                characterObject
                    .GetComponentsInChildren<Renderer>(
                        true));
        }

        private CharacterRuntimeEntry GetRequiredCharacterEntry(
            GameObject characterObject)
        {
            if (characterObject != null
                && _characterEntryByObject.TryGetValue(
                    characterObject,
                    out CharacterRuntimeEntry entry)
                && entry != null)
            {
                return entry;
            }

            string objectName =
                characterObject != null
                    ? characterObject.name
                    : "null";

            throw new InvalidOperationException(
                $"{objectName} の CharacterRuntimeEntry が存在しません。");
        }

        private void SubscribeCharacterDeath(
            CharacterRuntimeEntry entry)
        {
            CharacterStatus status =
                entry.Status;

            status.DiedStream
                .Subscribe(deadStatus =>
                {
                    if (deadStatus == null)
                    {
                        return;
                    }

                    GameObject deadObject =
                        deadStatus.gameObject;

                    _isBotRecoveringByCharacterObject.Remove(
                        deadObject);

                    SetCharacterRenderersEnabled(
                        entry,
                        false);

                    Debug.Log(
                        $"{deadObject.name} died.");

                    StartCoroutine(
                        RespawnCharacterAfterDelay(
                            deadStatus));
                })
                .AddTo(ref _disposables);
        }

        private IEnumerator RespawnCharacterAfterDelay(
            CharacterStatus status)
        {
            if (status == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(
                _respawnDelaySeconds);

            if (status == null)
            {
                yield break;
            }

            GameObject characterObject =
                status.gameObject;

            if (characterObject == null)
            {
                yield break;
            }

            if (!_spawnerByCharacterObject.TryGetValue(
                    characterObject,
                    out Component spawner)
                || spawner == null)
            {
                Debug.LogError(
                    $"{characterObject.name} の復活用 Spawner が見つかりません。");

                yield break;
            }

            Transform characterTransform =
                characterObject.transform;

            characterTransform.position =
                spawner.transform.position;

            characterTransform.rotation =
                spawner.transform.rotation;

            status.Respawn();

            characterObject.SetActive(true);

            CharacterRuntimeEntry entry =
                GetRequiredCharacterEntry(
                    characterObject);

            CacheCharacterRenderers(
                characterObject);

            SetCharacterRenderersEnabled(
                entry,
                true);

            _isBotRecoveringByCharacterObject[
                    characterObject] =
                false;

            Debug.Log(
                $"{characterObject.name} respawned.");
        }

        private void SubscribeBotControllers(
            GameObject playerObject)
        {
            int botIndex = 0;

            for (int i = 0;
                 i < _characterEntries.Count;
                 i++)
            {
                CharacterRuntimeEntry entry =
                    _characterEntries[i];

                GameObject obj =
                    entry.GameObject;

                if (obj == null)
                {
                    continue;
                }

                if (obj == playerObject)
                {
                    continue;
                }

                if (botIndex >= _botControllers.Length)
                {
                    Debug.LogWarning(
                        "BotController が不足しています。");

                    return;
                }

                BotController botController =
                    _botControllers[botIndex];

                Debug.Log(
                    $"Bot[{botIndex}] が操作するキャラクター: {obj.name}");

                SubscribeBotController(
                    botController,
                    entry);

                botIndex++;
            }
        }

        private void SubscribeBotController(
            BotController botController,
            CharacterRuntimeEntry entry)
        {
            GameObject characterObject =
                entry.GameObject;

            BotInputInterpreter inputInterpreter =
                botController
                    .GetComponent<BotInputInterpreter>();

            CharacterStatus status =
                entry.Status;

            inputInterpreter.Initialize(
                entry.Transform,
                FindNearestVisibleEnemyForBot);

            _isBotRecoveringByCharacterObject[
                    characterObject] =
                false;

            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    if (status == null)
                    {
                        return;
                    }

                    inputInterpreter.SetIsDead(
                        status.IsDead);

                    if (status.IsDead)
                    {
                        _isBotRecoveringByCharacterObject[
                                characterObject] =
                            false;

                        inputInterpreter.SetRecoveryMode(
                            false,
                            Vector3.zero,
                            0f);

                        return;
                    }

                    float hpRatio =
                        status.MaxHp > 0f
                            ? Mathf.Clamp01(
                                status.CurrentHp / status.MaxHp)
                            : 0f;

                    bool isRecovering =
                        _isBotRecoveringByCharacterObject
                            .TryGetValue(
                                characterObject,
                                out bool current)
                        && current;

                    if (!isRecovering
                        && hpRatio <= _botRecoveryEnterHpRatio)
                    {
                        isRecovering = true;
                    }
                    else if (isRecovering
                             && hpRatio >= _botRecoveryExitHpRatio)
                    {
                        isRecovering = false;
                    }

                    _isBotRecoveringByCharacterObject[
                            characterObject] =
                        isRecovering;

                    if (isRecovering)
                    {
                        Vector3 fleeDirectionWorld =
                            FindFleeDirectionWorld(
                                entry.Transform);

                        inputInterpreter.SetRecoveryMode(
                            true,
                            fleeDirectionWorld,
                            _botFleeMoveScale);
                    }
                    else
                    {
                        inputInterpreter.SetRecoveryMode(
                            false,
                            Vector3.zero,
                            0f);
                    }
                })
                .AddTo(ref _disposables);

            CharacterMove characterMove =
                GetRequiredComponent<CharacterMove>(
                    characterObject);

            CharacterAttackAim characterAttackAim =
                GetRequiredComponent<CharacterAttackAim>(
                    characterObject);

            CharacterSuperAim characterSuperAim =
                GetRequiredComponent<CharacterSuperAim>(
                    characterObject);

            CharacterAttack characterAttack =
                GetRequiredComponent<CharacterAttack>(
                    characterObject);

            Vector3 latestAimDirectionWorld =
                Vector3.forward;

            botController.MoveRequestedStream
                .Subscribe(request =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    characterMove.Move(
                        request.MoveDirectionWorld);
                })
                .AddTo(ref _disposables);

            botController.AimRequestedStream
                .Subscribe(request =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    if (request.AimDirectionWorld
                        .sqrMagnitude >
                        MinDirectionSqrMagnitude)
                    {
                        latestAimDirectionWorld =
                            request.AimDirectionWorld;
                    }

                    characterAttackAim.SetAim(
                        request.AimDirectionWorld,
                        request.AimPointWorld,
                        Vector2.zero);

                    characterSuperAim.SetAim(
                        request.AimDirectionWorld,
                        request.AimPointWorld,
                        Vector2.zero);
                })
                .AddTo(ref _disposables);

            botController.AttackRequestedStream
                .Subscribe(_ =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    RevealCharacterTemporarily(
                        characterObject);

                    characterAttack.Attack(
                        latestAimDirectionWorld);
                })
                .AddTo(ref _disposables);
        }

        private Vector3 FindFleeDirectionWorld(
            Transform selfTransform)
        {
            if (selfTransform == null)
            {
                return Vector3.zero;
            }

            GameObject nearestEnemy =
                FindNearestVisibleEnemyForBot(
                    selfTransform,
                    _playerAutoAimSearchRadius);

            if (nearestEnemy == null)
            {
                Vector3 forward =
                    selfTransform.forward;

                forward.y = 0f;

                return forward.sqrMagnitude > MinDirectionSqrMagnitude
                    ? forward.normalized
                    : Vector3.forward;
            }

            Vector3 away =
                selfTransform.position
                - nearestEnemy.transform.position;

            away.y = 0f;

            if (away.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                Vector3 fallback =
                    selfTransform.forward;

                fallback.y = 0f;

                return fallback.sqrMagnitude > MinDirectionSqrMagnitude
                    ? fallback.normalized
                    : Vector3.forward;
            }

            return away.normalized;
        }

        private void SubscribePlayerController(
            GameObject playerObject)
        {
            CharacterRuntimeEntry entry =
                GetRequiredCharacterEntry(
                    playerObject);

            CharacterStatus status =
                entry.Status;

            CharacterMove characterMove =
                GetRequiredComponent<CharacterMove>(
                    playerObject);

            CharacterAttackAim characterAttackAim =
                GetRequiredComponent<CharacterAttackAim>(
                    playerObject);

            CharacterSuperAim characterSuperAim =
                GetRequiredComponent<CharacterSuperAim>(
                    playerObject);

            CharacterAttack characterAttack =
                GetRequiredComponent<CharacterAttack>(
                    playerObject);

            CharacterSuper characterSuper =
                GetRequiredComponent<CharacterSuper>(
                    playerObject);

            Vector3 latestAimDirectionWorld =
                Vector3.forward;

            Vector3 releasedAttackDirectionWorld =
                Vector3.forward;

            Vector3 releasedSuperDirectionWorld =
                Vector3.forward;

            _playerController.MoveRequestedStream
                .Subscribe(request =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    characterMove.Move(
                        request.MoveDirectionWorld);
                })
                .AddTo(ref _disposables);

            _playerController.AimRequestedStream
                .Subscribe(request =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    if (request.AimDirectionWorld
                        .sqrMagnitude >
                        MinDirectionSqrMagnitude)
                    {
                        latestAimDirectionWorld =
                            request.AimDirectionWorld;
                    }

                    characterAttackAim.SetAim(
                        request.AimDirectionWorld,
                        request.AimPointWorld,
                        request.AimScreenPosition);

                    characterSuperAim.SetAim(
                        request.AimDirectionWorld,
                        request.AimPointWorld,
                        request.AimScreenPosition);
                })
                .AddTo(ref _disposables);

            _playerController.AttackInputRequestedStream
                .Subscribe(request =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    if (request.PressedThisFrame)
                    {
                        characterAttackAim.BeginAttackAim();
                    }

                    if (request.ReleasedThisFrame)
                    {
                        characterAttackAim.CompleteAttackAim();

                        releasedAttackDirectionWorld =
                            characterAttackAim
                                .GetTargetDirection();
                    }
                })
                .AddTo(ref _disposables);

            _playerController.SuperInputRequestedStream
                .Subscribe(request =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    if (request.PressedThisFrame)
                    {
                        characterSuperAim.BeginSuperAim();
                    }

                    if (request.ReleasedThisFrame)
                    {
                        characterSuperAim.CompleteSuperAim();

                        releasedSuperDirectionWorld =
                            characterSuperAim
                                .GetTargetDirection();
                    }
                })
                .AddTo(ref _disposables);

            _playerController.AttackRequestedStream
                .Subscribe(_ =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    Vector3 finalDirection =
                        ResolvePlayerAttackDirection(
                            playerObject,
                            characterAttackAim,
                            releasedAttackDirectionWorld,
                            latestAimDirectionWorld);

                    characterAttack.Attack(
                        finalDirection);
                })
                .AddTo(ref _disposables);

            _playerController.SuperRequestedStream
                .Subscribe(_ =>
                {
                    if (status.IsDead)
                    {
                        return;
                    }

                    Vector3 finalDirection =
                        ResolvePlayerSuperDirection(
                            playerObject,
                            characterSuperAim,
                            releasedSuperDirectionWorld,
                            latestAimDirectionWorld);

                    characterSuper.Super(
                        finalDirection);
                })
                .AddTo(ref _disposables);
        }

        private void ConfigureBushRevealSensors(
            TeamId visibleTeamId)
        {
            for (int i = 0;
                 i < _characterEntries.Count;
                 i++)
            {
                CharacterRuntimeEntry entry =
                    _characterEntries[i];

                GameObject characterObject =
                    entry.GameObject;

                if (characterObject == null)
                {
                    continue;
                }

                CharacterStatus characterStatus =
                    entry.Status;

                bool shouldRevealBush =
                    characterStatus.TeamId == visibleTeamId
                    && !characterStatus.IsDead;

                SetRevealEnabledIfChanged(
                    entry,
                    shouldRevealBush);
            }
        }

        private void SetRevealEnabledIfChanged(
            CharacterRuntimeEntry entry,
            bool isEnabled)
        {
            if (entry.HasRevealEnabledCache
                && entry.LastRevealEnabled == isEnabled)
            {
                return;
            }

            entry.Reveal.SetRevealEnabled(
                isEnabled);

            entry.LastRevealEnabled =
                isEnabled;

            entry.HasRevealEnabledCache =
                true;
        }

        private void UpdateCharacterVisibility()
        {
            ConfigureBushRevealSensors(
                _visibleTeamId);

            for (int i = 0;
                 i < _characterEntries.Count;
                 i++)
            {
                CharacterRuntimeEntry entry =
                    _characterEntries[i];

                GameObject targetObject =
                    entry.GameObject;

                if (targetObject == null)
                {
                    continue;
                }

                if (!targetObject.activeInHierarchy)
                {
                    continue;
                }

                UpdateCharacterInsideBushState(
                    entry);

                bool shouldRender =
                    ShouldRenderCharacter(
                        entry);

                SetCharacterRenderersEnabled(
                    entry,
                    shouldRender);

                SetCharacterHpUiVisibleIfChanged(
                    entry,
                    ShouldShowHpUi(entry));
            }
        }

        private void UpdateReloadBarVisibility(
            GameObject playerObject)
        {
            foreach (CharacterRuntimeEntry entry in _characterEntries)
            {
                if (entry.ReloadUi == null)
                {
                    continue;
                }

                bool isPlayer =
                    entry.GameObject == playerObject;

                entry.ReloadUi.SetUiVisible(
                    isPlayer);
            }
        }

        private bool ShouldShowHpUi(
            CharacterRuntimeEntry entry)
        {
            CharacterStatus status =
                entry.Status;

            if (status.TeamId == _visibleTeamId)
            {
                return true;
            }

            if (IsTemporarilyRevealed(entry.GameObject))
            {
                return true;
            }

            if (IsInsideVisibleTeamRevealRange(entry))
            {
                return true;
            }

            return !status.IsInsideBush;
        }

        private static void SetCharacterHpUiVisibleIfChanged(
            CharacterRuntimeEntry entry,
            bool isVisible)
        {
            if (entry == null
                || entry.HpUi == null)
            {
                return;
            }

            if (entry.HasHpUiVisibleCache
                && entry.LastHpUiVisible == isVisible)
            {
                return;
            }

            entry.HpUi.SetUiVisible(
                isVisible);

            entry.LastHpUiVisible =
                isVisible;

            entry.HasHpUiVisibleCache =
                true;
        }

        private static void UpdateCharacterInsideBushState(
            CharacterRuntimeEntry entry)
        {
            if (entry == null
                || entry.Status == null
                || entry.Reveal == null
                || entry.Transform == null)
            {
                return;
            }

            bool isInsideBush =
                entry.Reveal.IsInsideBush(
                    entry.Transform.position);

            if (entry.Status.IsInsideBush == isInsideBush)
            {
                return;
            }

            entry.Status.SetInsideBush(
                isInsideBush);

            Debug.Log(
                $"{entry.GameObject.name} Bush={isInsideBush}");
        }

        private void RevealCharacterTemporarily(
            GameObject characterObject)
        {
            if (characterObject == null)
            {
                return;
            }

            _temporaryRevealEndTimeByCharacterObject[
                characterObject] =
                Time.time + _attackRevealDuration;
        }

        private bool IsTemporarilyRevealed(
            GameObject characterObject)
        {
            return
                _temporaryRevealEndTimeByCharacterObject
                    .TryGetValue(
                        characterObject,
                        out float endTime)
                && Time.time < endTime;
        }

        private bool ShouldRenderCharacter(
            CharacterRuntimeEntry targetEntry)
        {
            CharacterStatus targetStatus =
        targetEntry.Status;

            return
                targetStatus.TeamId == _visibleTeamId
                || !targetStatus.IsInsideBush
                || IsTemporarilyRevealed(
                    targetEntry.GameObject)
                || IsInsideVisibleTeamRevealRange(
                    targetEntry);
        }

        private bool IsInsideVisibleTeamRevealRange(
            CharacterRuntimeEntry targetEntry)
        {
            Vector3 targetPosition =
                targetEntry.Transform.position;

            for (int i = 0;
                 i < _characterEntries.Count;
                 i++)
            {
                CharacterRuntimeEntry viewerEntry =
                    _characterEntries[i];

                GameObject viewerObject =
                    viewerEntry.GameObject;

                if (viewerObject == null
                    || viewerObject == targetEntry.GameObject)
                {
                    continue;
                }

                if (!viewerObject.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus viewerStatus =
                    viewerEntry.Status;

                if (viewerStatus.TeamId
                    != _visibleTeamId)
                {
                    continue;
                }

                if (viewerStatus.IsDead)
                {
                    continue;
                }

                CharacterReveal viewerReveal =
                    viewerEntry.Reveal;

                if (viewerReveal.ContainsWorldPosition(
                        targetPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private Renderer[] CacheCharacterRenderers(
            GameObject characterObject)
        {
            Renderer[] renderers =
                characterObject
                    .GetComponentsInChildren<Renderer>(
                        true);

            if (_characterEntryByObject.TryGetValue(
                    characterObject,
                    out CharacterRuntimeEntry entry)
                && entry != null)
            {
                entry.Renderers =
                    renderers;
            }

            return renderers;
        }

        private void SetCharacterRenderersEnabled(
            CharacterRuntimeEntry entry,
            bool isEnabled)
        {
            Renderer[] renderers =
                entry.Renderers;

            if (renderers == null)
            {
                renderers =
                    CacheCharacterRenderers(
                        entry.GameObject);
            }

            SetRenderersEnabled(
                renderers,
                isEnabled);
        }

        private static void SetRenderersEnabled(
            Renderer[] renderers,
            bool isEnabled)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (renderer.enabled == isEnabled)
                {
                    continue;
                }

                renderer.enabled =
                    isEnabled;
            }
        }

        private Component AssignCharacterToSpawnerPosition(
            GameObject characterObject,
            TeamId teamId)
        {
            if (characterObject == null)
            {
                throw new ArgumentNullException(
                    nameof(characterObject));
            }

            Component spawner =
                FindAndReserveSpawnerComponent(
                    teamId);

            Transform t =
                characterObject.transform;

            t.position =
                spawner.transform.position;

            t.rotation =
                spawner.transform.rotation;

            return spawner;
        }

        private Component FindAndReserveSpawnerComponent(
            TeamId teamId)
        {
            EnsureSpawnerReflectionCache();

            UnityEngine.Object[] foundObjects =
                FindObjectsByType(
                    _cachedSpawnerType,
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            if (foundObjects == null
                || foundObjects.Length == 0)
            {
                throw new InvalidOperationException(
                    "Spawner が Scene 上に存在しません。" +
                    " `Uraty.Features.Terrain.Spawner` を配置してください。");
            }

            for (int i = 0;
                 i < foundObjects.Length;
                 i++)
            {
                if (foundObjects[i] is not Component spawner)
                {
                    continue;
                }

                if (_spawnerLayerMask.value != 0)
                {
                    int spawnerLayerBit =
                        1 << spawner.gameObject.layer;

                    bool isTargetLayer =
                        (_spawnerLayerMask.value
                         & spawnerLayerBit) != 0;

                    if (!isTargetLayer)
                    {
                        continue;
                    }
                }

                object propertyValue =
                    _cachedSpawnerTeamIdProperty.GetValue(
                        spawner,
                        null);

                if (propertyValue is not TeamId spawnerTeamId
                    || spawnerTeamId != teamId)
                {
                    continue;
                }

                bool reserved =
                    (bool)_cachedSpawnerTryReserveMethod.Invoke(
                        spawner,
                        null);

                if (!reserved)
                {
                    continue;
                }

                return spawner;
            }

            throw new InvalidOperationException(
                $"TeamId={teamId} の未使用スポナーが見つかりません。" +
                " (数が足りない /既に使用済み / LayerMask が誤っている可能性があります)");
        }

        private static void EnsureSpawnerReflectionCache()
        {
            if (_isSpawnerReflectionCached)
            {
                return;
            }

            _cachedSpawnerType =
                Type.GetType(SpawnerTypeName)
                ?? ResolveTypeFromLoadedAssemblies(
                    SpawnerTypeName);

            if (_cachedSpawnerType == null)
            {
                throw new InvalidOperationException(
                    $"{SpawnerTypeName} が見つかりません。" +
                    " Terrain 側の asmdef /参照設定を確認してください。");
            }

            _cachedSpawnerTeamIdProperty =
                _cachedSpawnerType.GetProperty(
                    "TeamId",
                    BindingFlags.Instance
                    | BindingFlags.Public);

            _cachedSpawnerTryReserveMethod =
                _cachedSpawnerType.GetMethod(
                    "TryReserve",
                    BindingFlags.Instance
                    | BindingFlags.Public);

            if (_cachedSpawnerTeamIdProperty == null
                || _cachedSpawnerTryReserveMethod == null)
            {
                throw new InvalidOperationException(
                    $"{SpawnerTypeName} のメンバーが見つかりません。" +
                    " TeamId プロパティと TryReserve メソッドが必要です。");
            }

            _isSpawnerReflectionCached =
                true;
        }

        private static Type ResolveTypeFromLoadedAssemblies(
            string fullName)
        {
            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0;
                 i < assemblies.Length;
                 i++)
            {
                Assembly assembly =
                    assemblies[i];

                if (assembly == null)
                {
                    continue;
                }

                Type type =
                    assembly.GetType(
                        fullName,
                        throwOnError: false);

                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private bool HasCharacterPrefab(
            RoleType roleType)
        {
            if (_roleCharacterPrefabEntries == null)
            {
                return false;
            }

            foreach (RoleCharacterPrefabEntry entry
                     in _roleCharacterPrefabEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.RoleType != roleType)
                {
                    continue;
                }

                return entry.CharacterPrefab != null;
            }

            return false;
        }

        private GameObject FindCharacterPrefab(
            RoleType roleType)
        {
            foreach (RoleCharacterPrefabEntry entry
                     in _roleCharacterPrefabEntries)
            {
                if (entry.RoleType == roleType)
                {
                    return entry.CharacterPrefab;
                }
            }

            throw new InvalidOperationException(
                $"{roleType} のPrefab未登録");
        }

        private static T GetRequiredComponent<T>(
            GameObject target)
            where T : Component
        {
            if (target.TryGetComponent(
                    out T component))
            {
                return component;
            }

            throw new InvalidOperationException(
                $"{target.name} に {typeof(T).Name} が存在しません。");
        }

        private bool CanCharacterBeSeen(
            CharacterRuntimeEntry viewerEntry,
            CharacterRuntimeEntry targetEntry)
        {
            if (viewerEntry == null
                || targetEntry == null)
            {
                return false;
            }

            CharacterStatus viewerStatus =
                viewerEntry.Status;

            CharacterStatus targetStatus =
                targetEntry.Status;

            if (viewerStatus == null
                || targetStatus == null)
            {
                return false;
            }

            if (targetStatus.IsDead)
            {
                return false;
            }

            if (!targetStatus.IsInsideBush)
            {
                return true;
            }

            if (viewerStatus.TeamId
                == targetStatus.TeamId)
            {
                return true;
            }

            return viewerEntry.Reveal.ContainsWorldPosition(
                targetEntry.Transform.position);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();

            _characterEntries.Clear();
            _characterEntryByObject.Clear();
            _isBotRecoveringByCharacterObject.Clear();
            _spawnerByCharacterObject.Clear();
        }

        private GameObject FindNearestVisibleEnemyForBot(
            Transform selfTransform,
            float searchRadius)
        {
            if (selfTransform == null)
            {
                return null;
            }

            float searchRadiusSqr =
                Mathf.Max(0f, searchRadius);

            searchRadiusSqr *= searchRadiusSqr;

            GameObject nearest = null;
            float nearestSqrDistance = float.MaxValue;

            CharacterRuntimeEntry selfEntry =
                GetRequiredCharacterEntry(
                    selfTransform.gameObject);

            CharacterStatus selfStatus =
                selfEntry.Status;

            for (int i = 0;
                 i < _characterEntries.Count;
                 i++)
            {
                CharacterRuntimeEntry otherEntry =
                    _characterEntries[i];

                GameObject otherObject =
                    otherEntry.GameObject;

                if (otherObject == null)
                {
                    continue;
                }

                if (otherEntry.Transform == selfTransform)
                {
                    continue;
                }

                if (!otherObject.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus otherStatus =
                    otherEntry.Status;

                if (otherStatus.IsDead)
                {
                    continue;
                }

                if (otherStatus.TeamId == selfStatus.TeamId)
                {
                    continue;
                }

                if (otherStatus.IsInsideBush)
                {
                    continue;
                }

                if (!CanCharacterBeSeen(selfEntry, otherEntry))
                {
                    continue;
                }

                Vector3 diff =
                    otherEntry.Transform.position
                    - selfTransform.position;

                diff.y = 0f;

                float sqrDistance =
                    diff.sqrMagnitude;

                if (sqrDistance > searchRadiusSqr)
                {
                    continue;
                }

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance =
                        sqrDistance;

                    nearest =
                        otherObject;
                }
            }

            return nearest;
        }

        private Vector3 ResolvePlayerAttackDirection(
            GameObject playerObject,
            CharacterAttackAim aim,
            Vector3 releasedDirectionFallback,
            Vector3 latestAimDirectionWorld)
        {
            if (aim != null)
            {
                if (aim.TryConsumeAttack(
                        out _,
                        out Vector3 consumedDirection,
                        out bool canAutoAim))
                {
                    return ResolveDirectionWithAutoAim(
                        playerObject,
                        consumedDirection,
                        canAutoAim);
                }
            }

            Vector3 direction =
                releasedDirectionFallback.sqrMagnitude
                > MinDirectionSqrMagnitude
                    ? releasedDirectionFallback
                    : latestAimDirectionWorld;

            return ResolveDirectionWithAutoAim(
                playerObject,
                direction,
                canAutoAim: false);
        }

        private Vector3 ResolvePlayerSuperDirection(
            GameObject playerObject,
            CharacterSuperAim aim,
            Vector3 releasedDirectionFallback,
            Vector3 latestAimDirectionWorld)
        {
            if (aim != null)
            {
                if (aim.TryConsumeSuper(
                        out _,
                        out Vector3 consumedDirection,
                        out bool canAutoAim))
                {
                    return ResolveDirectionWithAutoAim(
                        playerObject,
                        consumedDirection,
                        canAutoAim);
                }
            }

            Vector3 direction =
                releasedDirectionFallback.sqrMagnitude
                > MinDirectionSqrMagnitude
                    ? releasedDirectionFallback
                    : latestAimDirectionWorld;

            return ResolveDirectionWithAutoAim(
                playerObject,
                direction,
                canAutoAim: false);
        }

        private Vector3 ResolveDirectionWithAutoAim(
            GameObject playerObject,
            Vector3 baseDirectionWorld,
            bool canAutoAim)
        {
            if (playerObject == null)
            {
                return baseDirectionWorld;
            }

            baseDirectionWorld.y = 0f;

            if (!canAutoAim
                && baseDirectionWorld.sqrMagnitude
                > MinDirectionSqrMagnitude)
            {
                return baseDirectionWorld.normalized;
            }

            Vector3 forward =
                playerObject.transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude
                <= MinDirectionSqrMagnitude)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            if (!canAutoAim)
            {
                return forward;
            }

            GameObject enemy =
                FindNearestEnemyInCone(
                    playerObject.transform,
                    forward,
                    _playerAutoAimSearchRadius,
                    _playerAutoAimMaxAngleDegrees);

            if (enemy == null)
            {
                return forward;
            }

            Vector3 toEnemy =
                enemy.transform.position
                - playerObject.transform.position;

            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude
                <= MinDirectionSqrMagnitude)
            {
                return forward;
            }

            return toEnemy.normalized;
        }

        private GameObject FindNearestEnemyInCone(
            Transform selfTransform,
            Vector3 forward,
            float searchRadius,
            float maxAngleDegrees)
        {
            if (selfTransform == null)
            {
                return null;
            }

            CharacterRuntimeEntry selfEntry =
                GetRequiredCharacterEntry(
                    selfTransform.gameObject);

            CharacterStatus selfStatus =
                selfEntry.Status;

            float radius =
                Mathf.Max(0f, searchRadius);

            float radiusSqr =
                radius * radius;

            float cosThreshold =
                Mathf.Cos(
                    Mathf.Clamp(
                        maxAngleDegrees,
                        0f,
                        180f) * Mathf.Deg2Rad);

            GameObject nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0;
                 i < _characterEntries.Count;
                 i++)
            {
                CharacterRuntimeEntry otherEntry =
                    _characterEntries[i];

                GameObject other =
                    otherEntry.GameObject;

                if (other == null
                    || otherEntry.Transform == selfTransform
                    || !other.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus otherStatus =
                    otherEntry.Status;

                if (otherStatus.IsDead)
                {
                    continue;
                }

                if (otherStatus.TeamId
                    == selfStatus.TeamId)
                {
                    continue;
                }

                if (otherStatus.IsInsideBush)
                {
                    continue;
                }

                Vector3 diff =
                    otherEntry.Transform.position
                    - selfTransform.position;

                diff.y = 0f;

                float sqr =
                    diff.sqrMagnitude;

                if (sqr > radiusSqr
                    || sqr <= MinDirectionSqrMagnitude)
                {
                    continue;
                }

                Vector3 dir =
                    diff.normalized;

                float dot =
                    Vector3.Dot(
                        forward,
                        dir);

                if (dot < cosThreshold)
                {
                    continue;
                }

                if (sqr < nearestSqr)
                {
                    nearestSqr =
                        sqr;

                    nearest =
                        other;
                }
            }

            return nearest;
        }

        [Serializable]
        private sealed class RoleCharacterPrefabEntry
        {
            [SerializeField]
            private RoleType _roleType;

            [SerializeField]
            private GameObject _characterPrefab;

            public RoleType RoleType =>
                _roleType;

            public GameObject CharacterPrefab =>
                _characterPrefab;
        }

        private sealed class CharacterRuntimeEntry
        {
            public CharacterRuntimeEntry(
                GameObject gameObject,
                Transform transform,
                CharacterStatus status,
                CharacterReveal reveal,
                HPSystem hpUi,
                CharacterReloadBar reloadUi,
                Renderer[] renderers)
            {
                GameObject =
                    gameObject;

                Transform =
                    transform;

                Status =
                    status;

                Reveal =
                    reveal;

                HpUi =
                    hpUi;

                ReloadUi =
                    reloadUi;

                Renderers =
                    renderers;
            }

            public GameObject GameObject
            {
                get;
            }

            public Transform Transform
            {
                get;
            }

            public CharacterStatus Status
            {
                get;
            }

            public CharacterReveal Reveal
            {
                get;
            }

            public HPSystem HpUi
            {
                get;
            }

            public CharacterReloadBar ReloadUi
            {
                get;
            }

            public Renderer[] Renderers
            {
                get;
                set;
            }

            public bool HasRevealEnabledCache
            {
                get;
                set;
            }

            public bool LastRevealEnabled
            {
                get;
                set;
            }

            public bool HasHpUiVisibleCache
            {
                get;
                set;
            }

            public bool LastHpUiVisible
            {
                get;
                set;
            }

            public bool HasReloadUiVisibleCache
            {
                get;
                set;
            }

            public bool LastReloadUiVisible
            {
                get;
                set;
            }
        }
    }
}
