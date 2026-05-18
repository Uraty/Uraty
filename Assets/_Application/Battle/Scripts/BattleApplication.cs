using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using R3;

using UnityEngine;

using Uraty.Features.Bot;
using Uraty.Features.Character;
using Uraty.Features.Player;
using Uraty.Shared.Team;
using Uraty.Systems.Camera;
using Uraty.Systems.Input;

namespace Uraty.Application.Battle
{
    public sealed class BattleApplication : MonoBehaviour
    {
        [Header("Auto Aim (Player)")]
        [SerializeField, Min(0f)]
        private float _playerAutoAimSearchRadius = 12f;

        [SerializeField, Range(0f, 180f)]
        private float _playerAutoAimMaxAngleDegrees = 55f;

        private const int TeamMemberCount = 3;
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("Bot Recovery")]
        [Tooltip("Botが逃走・回復に入るHP比率(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _botRecoveryEnterHpRatio = 0.5f;

        [Tooltip("回復開始後、このHP比率まで回復したら通常行動へ戻る(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _botRecoveryExitHpRatio = 0.7f;

        [Tooltip("逃走中の自然回復量(HP/秒)")]
        [SerializeField, Min(0f)]
        private float _botRecoveryHealPerSecond = 8f;

        [Tooltip("逃走移動の強さ(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _botFleeMoveScale = 1.0f;

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

        private readonly List<GameObject>
            _characterObjects = new();

        private readonly Dictionary<GameObject, Renderer[]>
            _characterRenderersByObject = new();

        private DisposableBag _disposables;

        // Bot毎の「回復モード」状態
        private readonly Dictionary<GameObject, bool> _isBotRecoveringByCharacterObject = new();

        private IEnumerator Start()
        {
            yield return null;

            _input.Player.Enable();

            RoleType[] roleTypes =
                (RoleType[])Enum.GetValues(
                    typeof(RoleType));

            int selectedIndex =
                Array.IndexOf(
                    roleTypes,
                    _fallbackPlayerRoleType);

            GameObject playerObject =
                SpawnPlayerTeam(
                    roleTypes,
                    selectedIndex);

            SpawnEnemyTeam(
                roleTypes,
                selectedIndex);

            ConfigureBushRevealSensors(
                _visibleTeamId);

            _playerCamera
                .GetComponent<CameraMove>()
                .SetTarget(playerObject);

            SubscribePlayerController(
                playerObject);

            SubscribeBotControllers(
                playerObject);
        }

        private void Update()
        {
            UpdateCharacterVisibility();
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

            AssignCharacterToSpawnerPosition(
                obj,
                teamId);

            CharacterStatus status =
                GetRequiredComponent<CharacterStatus>(
                    obj);

            status.Initialize(teamId);

            _characterObjects.Add(obj);

            CacheCharacterRenderers(obj);

            return obj;
        }

        private void SubscribeBotControllers(
            GameObject playerObject)
        {
            int botIndex = 0;

            for (int i = 0;
                 i < _characterObjects.Count;
                 i++)
            {
                GameObject obj =
                    _characterObjects[i];

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
                    obj);

                botIndex++;
            }
        }

        private void SubscribeBotController(
            BotController botController,
            GameObject characterObject)
        {
            BotInputInterpreter inputInterpreter =
                botController
                    .GetComponent<BotInputInterpreter>();

            CharacterStatus status =
                GetRequiredComponent<CharacterStatus>(
                    characterObject);

            // BotInputInterpreter が CharacterStatus を参照しないように、
            // 必要最小限の情報（Transform と敵探索関数）だけを注入する。
            inputInterpreter.Initialize(
                characterObject.transform,
                FindNearestVisibleEnemyForBot);

            // 初期状態
            _isBotRecoveringByCharacterObject[characterObject] = false;

            // 毎フレーム Application 側の状態を注入
            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    if (status == null)
                    {
                        return;
                    }

                    inputInterpreter.SetIsDead(status.IsDead);

                    if (status.IsDead)
                    {
                        _isBotRecoveringByCharacterObject[characterObject] = false;
                        inputInterpreter.SetRecoveryMode(false, Vector3.zero, 0f);
                        return;
                    }

                    float hpRatio = status.MaxHp > 0f
                        ? Mathf.Clamp01(status.CurrentHp / status.MaxHp)
                        : 0f;

                    bool isRecovering = _isBotRecoveringByCharacterObject.TryGetValue(characterObject, out bool current)
                        && current;

                    if (!isRecovering && hpRatio <= _botRecoveryEnterHpRatio)
                    {
                        isRecovering = true;
                    }
                    else if (isRecovering && hpRatio >= _botRecoveryExitHpRatio)
                    {
                        isRecovering = false;
                    }

                    _isBotRecoveringByCharacterObject[characterObject] = isRecovering;

                    if (isRecovering)
                    {
                        //逃げながら自然回復
                        status.Heal(_botRecoveryHealPerSecond * Time.deltaTime);

                        Vector3 fleeDirectionWorld = FindFleeDirectionWorld(characterObject.transform);
                        inputInterpreter.SetRecoveryMode(true, fleeDirectionWorld, _botFleeMoveScale);
                    }
                    else
                    {
                        inputInterpreter.SetRecoveryMode(false, Vector3.zero, 0f);
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

            CharacterSuper characterSuper =
                GetRequiredComponent<CharacterSuper>(
                    characterObject);

            Vector3 latestAimDirectionWorld =
                Vector3.forward;

            Vector3 releasedAttackDirectionWorld = Vector3.forward;
            Vector3 releasedSuperDirectionWorld = Vector3.forward;

            botController.MoveRequestedStream
                .Subscribe(request =>
                {
                    characterMove.Move(
                        request.MoveDirectionWorld);
                })
                .AddTo(ref _disposables);

            botController.AimRequestedStream
                .Subscribe(request =>
                {
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
                    // BotはAimプレビュー/Completeを経由しないため、従来通り最新Aim方向で発射
                    characterAttack.Attack(latestAimDirectionWorld);
                })
                .AddTo(ref _disposables);
        }

        private Vector3 FindFleeDirectionWorld(Transform selfTransform)
        {
            if (selfTransform == null)
            {
                return Vector3.zero;
            }

            GameObject nearestEnemy = FindNearestVisibleEnemyForBot(selfTransform, _playerAutoAimSearchRadius);
            if (nearestEnemy == null)
            {
                // 敵が見えていないなら、いったん前方へ
                Vector3 forward = selfTransform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > MinDirectionSqrMagnitude
                    ? forward.normalized
                    : Vector3.forward;
            }

            Vector3 away = selfTransform.position - nearestEnemy.transform.position;
            away.y = 0f;

            if (away.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                Vector3 fallback = selfTransform.forward;
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

            // ボタン解放（CompleteAim）時点で確定した発射方向。
            // Aim入力が無い場合でも CharacterAim 側の fallbackで決まるため、オートエイム復活に利用する。
            Vector3 releasedAttackDirectionWorld = Vector3.forward;
            Vector3 releasedSuperDirectionWorld = Vector3.forward;

            _playerController.MoveRequestedStream
                .Subscribe(request =>
                {
                    characterMove.Move(
                        request.MoveDirectionWorld);
                })
                .AddTo(ref _disposables);

            _playerController.AimRequestedStream
                .Subscribe(request =>
                {
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

            //ここで「ボタン押下中にエイム線を出す」を復活させる
            _playerController.AttackInputRequestedStream
                .Subscribe(request =>
                {
                    if (request.PressedThisFrame)
                    {
                        characterAttackAim.BeginAttackAim();
                    }

                    if (request.ReleasedThisFrame)
                    {
                        characterAttackAim.CompleteAttackAim();

                        // CompleteAimで確定した方向を取得（Aim入力が無い場合は fallback方向=オートエイム対象）
                        releasedAttackDirectionWorld = characterAttackAim.GetTargetDirection();
                    }
                })
                .AddTo(ref _disposables);

            _playerController.SuperInputRequestedStream
                .Subscribe(request =>
                {
                    if (request.PressedThisFrame)
                    {
                        characterSuperAim.BeginSuperAim();
                    }

                    if (request.ReleasedThisFrame)
                    {
                        characterSuperAim.CompleteSuperAim();

                        // CompleteAimで確定した方向を取得
                        releasedSuperDirectionWorld = characterSuperAim.GetTargetDirection();
                    }
                })
                .AddTo(ref _disposables);

            _playerController.AttackRequestedStream
                .Subscribe(_ =>
                {
                    Vector3 finalDirection = ResolvePlayerAttackDirection(
                        playerObject,
                        characterAttackAim,
                        releasedAttackDirectionWorld,
                        latestAimDirectionWorld);

                    characterAttack.Attack(finalDirection);
                })
                .AddTo(ref _disposables);

            _playerController.SuperRequestedStream
                .Subscribe(_ =>
                {
                    Vector3 finalDirection = ResolvePlayerSuperDirection(
                        playerObject,
                        characterSuperAim,
                        releasedSuperDirectionWorld,
                        latestAimDirectionWorld);

                    characterSuper.Super(finalDirection);
                })
                .AddTo(ref _disposables);
        }

        private void ConfigureBushRevealSensors(
            TeamId visibleTeamId)
        {
            for (int i = 0;
                 i < _characterObjects.Count;
                 i++)
            {
                GameObject characterObject =
                    _characterObjects[i];

                if (characterObject == null)
                {
                    continue;
                }

                CharacterStatus characterStatus =
                    GetRequiredComponent<CharacterStatus>(
                        characterObject);

                CharacterReveal revealSensor =
                    GetRequiredComponent<CharacterReveal>(
                        characterObject);

                bool shouldRevealBush =
                    characterStatus.TeamId
                    == visibleTeamId
                    && !characterStatus.IsDead;

                revealSensor.SetRevealEnabled(
                    shouldRevealBush);
            }
        }

        private void UpdateCharacterVisibility()
        {
            ConfigureBushRevealSensors(
                _visibleTeamId);

            for (int i = 0;
                 i < _characterObjects.Count;
                 i++)
            {
                GameObject targetObject =
                    _characterObjects[i];

                if (targetObject == null)
                {
                    continue;
                }

                if (!targetObject.activeInHierarchy)
                {
                    continue;
                }

                bool shouldRender =
                    ShouldRenderCharacter(
                        targetObject);

                SetCharacterRenderersEnabled(
                    targetObject,
                    shouldRender);
            }
        }

        private bool ShouldRenderCharacter(
            GameObject targetObject)
        {
            CharacterStatus targetStatus =
                GetRequiredComponent<CharacterStatus>(
                    targetObject);

            return
                targetStatus.TeamId
                == _visibleTeamId
                || !targetStatus.IsInsideBush
                || IsInsideVisibleTeamRevealRange(
                    targetObject);
        }

        private bool IsInsideVisibleTeamRevealRange(
            GameObject targetObject)
        {
            Vector3 targetPosition =
                targetObject.transform.position;

            for (int i = 0;
                 i < _characterObjects.Count;
                 i++)
            {
                GameObject viewerObject =
                    _characterObjects[i];

                if (viewerObject == null
                    || viewerObject == targetObject)
                {
                    continue;
                }

                if (!viewerObject.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus viewerStatus =
                    GetRequiredComponent<CharacterStatus>(
                        viewerObject);

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
                    GetRequiredComponent<CharacterReveal>(
                        viewerObject);

                if (viewerReveal.ContainsWorldPosition(
                        targetPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheCharacterRenderers(
            GameObject characterObject)
        {
            Renderer[] renderers =
                characterObject
                    .GetComponentsInChildren<Renderer>(
                        true);

            _characterRenderersByObject[
                characterObject] =
                renderers;
        }

        private void SetCharacterRenderersEnabled(
            GameObject characterObject,
            bool isEnabled)
        {
            if (!_characterRenderersByObject
                    .TryGetValue(
                        characterObject,
                        out Renderer[] renderers))
            {
                CacheCharacterRenderers(
                    characterObject);

                renderers =
                    _characterRenderersByObject[
                        characterObject];
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

                renderer.enabled =
                    isEnabled;
            }
        }

        private void AssignCharacterToSpawnerPosition(
            GameObject characterObject,
            TeamId teamId)
        {
            if (characterObject == null)
            {
                return;
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
        }

        private Component FindAndReserveSpawnerComponent(
            TeamId teamId)
        {
            const string spawnerTypeName =
                "Uraty.Features.Terrain.Spawner";

            // `Type.GetType` はアセンブリ名無しだと nullになることがあるため、
            // 全アセンブリを探索して型を見つける。
            Type spawnerType =
                Type.GetType(spawnerTypeName)
                ?? ResolveTypeFromLoadedAssemblies(
                    spawnerTypeName);

            if (spawnerType == null)
            {
                throw new InvalidOperationException(
                    $"{spawnerTypeName} が見つかりません。" +
                    " Terrain 側の asmdef /参照設定を確認してください。"
                );
            }

            Component[] spawners =
                (Component[])FindObjectsByType(
                    spawnerType,
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            if (spawners == null || spawners.Length == 0)
            {
                throw new InvalidOperationException(
                    "Spawner が Scene 上に存在しません。" +
                    " `Uraty.Features.Terrain.Spawner` を配置してください。"
                );
            }

            PropertyInfo teamIdProperty =
                spawnerType.GetProperty(
                    "TeamId",
                    BindingFlags.Instance
                    | BindingFlags.Public);

            MethodInfo tryReserveMethod =
                spawnerType.GetMethod(
                    "TryReserve",
                    BindingFlags.Instance
                    | BindingFlags.Public);

            if (teamIdProperty == null || tryReserveMethod == null)
            {
                throw new InvalidOperationException(
                    $"{spawnerTypeName} のメンバーが見つかりません。" +
                    " TeamId プロパティと TryReserve メソッドが必要です。"
                );
            }

            for (int i = 0;
                 i < spawners.Length;
                 i++)
            {
                Component spawner =
                    spawners[i];

                if (spawner == null)
                {
                    continue;
                }

                // LayerMask が指定されている場合のみフィルタ
                if (_spawnerLayerMask.value != 0)
                {
                    int spawnerLayerBit = 1 << spawner.gameObject.layer;
                    bool isTargetLayer =
                        (_spawnerLayerMask.value & spawnerLayerBit) != 0;

                    if (!isTargetLayer)
                    {
                        continue;
                    }
                }

                object propertyValue =
                    teamIdProperty.GetValue(
                        spawner,
                        null);

                if (propertyValue
                    is not TeamId spawnerTeamId
                    || spawnerTeamId != teamId)
                {
                    continue;
                }

                bool reserved =
                    (bool)tryReserveMethod.Invoke(
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
                " (数が足りない /既に使用済み / LayerMask が誤っている可能性があります)"
            );
        }

        private static Type ResolveTypeFromLoadedAssemblies(
            string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];

                if (assembly == null)
                {
                    continue;
                }

                Type type = assembly.GetType(fullName, throwOnError: false);

                if (type != null)
                {
                    return type;
                }
            }

            return null;
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

        private void OnDestroy()
        {
            _disposables.Dispose();

            _characterRenderersByObject.Clear();
            _isBotRecoveringByCharacterObject.Clear();
        }

        private GameObject FindNearestVisibleEnemyForBot(
            Transform selfTransform,
            float searchRadius)
        {
            if (selfTransform == null)
            {
                return null;
            }

            float searchRadiusSqr = Mathf.Max(0f, searchRadius);
            searchRadiusSqr *= searchRadiusSqr;

            GameObject nearest = null;
            float nearestSqrDistance = float.MaxValue;

            // BattleApplication が生成/管理しているキャラクターだけを対象にする
            for (int i = 0; i < _characterObjects.Count; i++)
            {
                GameObject otherObject = _characterObjects[i];
                if (otherObject == null)
                {
                    continue;
                }

                if (otherObject.transform == selfTransform)
                {
                    continue;
                }

                if (!otherObject.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus otherStatus =
                    GetRequiredComponent<CharacterStatus>(
                        otherObject);

                // Dead
                if (otherStatus.IsDead)
                {
                    continue;
                }

                CharacterStatus selfStatus =
                    GetRequiredComponent<CharacterStatus>(
                        selfTransform.gameObject);

                // Same team
                if (otherStatus.TeamId == selfStatus.TeamId)
                {
                    continue;
                }

                // Bush (暫定: Bot はブッシュ内の敵を無視)
                if (otherStatus.IsInsideBush)
                {
                    continue;
                }

                Vector3 diff = otherObject.transform.position - selfTransform.position;
                diff.y = 0f;

                float sqrDistance = diff.sqrMagnitude;

                if (sqrDistance > searchRadiusSqr)
                {
                    continue;
                }

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = otherObject;
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
                Vector3 consumedAimPoint;
                Vector3 consumedDirection;
                bool canAutoAim;

                if (aim.TryConsumeAttack(out consumedAimPoint, out consumedDirection, out canAutoAim))
                {
                    return ResolveDirectionWithAutoAim(playerObject, consumedDirection, canAutoAim);
                }
            }

            Vector3 direction = releasedDirectionFallback.sqrMagnitude > MinDirectionSqrMagnitude
                ? releasedDirectionFallback
                : latestAimDirectionWorld;

            return ResolveDirectionWithAutoAim(playerObject, direction, canAutoAim: false);
        }

        private Vector3 ResolvePlayerSuperDirection(
            GameObject playerObject,
            CharacterSuperAim aim,
            Vector3 releasedDirectionFallback,
            Vector3 latestAimDirectionWorld)
        {
            if (aim != null)
            {
                Vector3 consumedAimPoint;
                Vector3 consumedDirection;
                bool canAutoAim;

                if (aim.TryConsumeSuper(out consumedAimPoint, out consumedDirection, out canAutoAim))
                {
                    return ResolveDirectionWithAutoAim(playerObject, consumedDirection, canAutoAim);
                }
            }

            Vector3 direction = releasedDirectionFallback.sqrMagnitude > MinDirectionSqrMagnitude
                ? releasedDirectionFallback
                : latestAimDirectionWorld;

            return ResolveDirectionWithAutoAim(playerObject, direction, canAutoAim: false);
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

            // Aim入力がある場合はそのまま撃つ
            if (!canAutoAim && baseDirectionWorld.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                return baseDirectionWorld.normalized;
            }

            // AutoAim中は、最低でも「向いている方向」で発射する
            Vector3 forward = playerObject.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            if (!canAutoAim)
            {
                return forward;
            }

            // canAutoAim=true のときだけ敵へ吸い付ける（味方は除外）
            GameObject enemy = FindNearestEnemyInCone(
                playerObject.transform,
                forward,
                _playerAutoAimSearchRadius,
                _playerAutoAimMaxAngleDegrees);

            if (enemy == null)
            {
                return forward;
            }

            Vector3 toEnemy = enemy.transform.position - playerObject.transform.position;
            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude <= MinDirectionSqrMagnitude)
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

            CharacterStatus selfStatus = GetRequiredComponent<CharacterStatus>(selfTransform.gameObject);

            float radius = Mathf.Max(0f, searchRadius);
            float radiusSqr = radius * radius;
            float cosThreshold = Mathf.Cos(Mathf.Clamp(maxAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);

            GameObject nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < _characterObjects.Count; i++)
            {
                GameObject other = _characterObjects[i];
                if (other == null || other.transform == selfTransform || !other.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus otherStatus = GetRequiredComponent<CharacterStatus>(other);
                if (otherStatus.IsDead)
                {
                    continue;
                }

                // 味方は対象外
                if (otherStatus.TeamId == selfStatus.TeamId)
                {
                    continue;
                }

                // ブッシュ内は暫定で対象外（視認できない想定）
                if (otherStatus.IsInsideBush)
                {
                    continue;
                }

                Vector3 diff = other.transform.position - selfTransform.position;
                diff.y = 0f;

                float sqr = diff.sqrMagnitude;
                if (sqr > radiusSqr || sqr <= MinDirectionSqrMagnitude)
                {
                    continue;
                }

                Vector3 dir = diff.normalized;
                float dot = Vector3.Dot(forward, dir);
                if (dot < cosThreshold)
                {
                    continue;
                }

                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = other;
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
    }
}
