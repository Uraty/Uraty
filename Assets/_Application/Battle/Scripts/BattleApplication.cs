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
    /// <summary>
    /// バトルシーン全体の生成、入力接続、Bot制御、可視性制御、復活処理を管理するアプリケーション層のコンポーネントです。
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

        /// <summary>
        /// Reflection で解決した Spawner 型のキャッシュです。
        /// </summary>
        private static Type _cachedSpawnerType;

        /// <summary>
        /// Spawner.TeamId プロパティ情報のキャッシュです。
        /// </summary>
        private static PropertyInfo _cachedSpawnerTeamIdProperty;

        /// <summary>
        /// Spawner.TryReserve メソッド情報のキャッシュです。
        /// </summary>
        private static MethodInfo _cachedSpawnerTryReserveMethod;

        /// <summary>
        /// Spawner の Reflection 情報をキャッシュ済みかどうかです。
        /// </summary>
        private static bool _isSpawnerReflectionCached;

        /// <summary>
        /// プレイヤーのオートエイム対象を探索する半径です。
        /// </summary>
        [Header("Auto Aim (Player)")]
        [SerializeField, Min(0f)]
        private float _playerAutoAimSearchRadius = 12f;

        /// <summary>
        /// プレイヤーのオートエイム対象を許可する最大角度です。
        /// </summary>
        [SerializeField, Range(0f, 180f)]
        private float _playerAutoAimMaxAngleDegrees = 55f;

        /// <summary>
        /// Botが逃走・回復に入るHP比率です。
        /// </summary>
        [Header("Bot Recovery")]
        [Tooltip("Botが逃走・回復に入るHP比率(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _botRecoveryEnterHpRatio = 0.5f;

        /// <summary>
        /// Botが通常行動へ戻るHP比率です。
        /// </summary>
        [Tooltip("回復開始後、このHP比率まで回復したら通常行動へ戻る(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _botRecoveryExitHpRatio = 0.7f;

        /// <summary>
        /// Botの逃走移動の強さです。
        /// </summary>
        [Tooltip("逃走移動の強さ(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _botFleeMoveScale = 1.0f;

        /// <summary>
        /// 死亡してから復活するまでの秒数です。
        /// </summary>
        [Header("Respawn")]
        [SerializeField, Min(0f)]
        private float _respawnDelaySeconds = 3f;

        /// <summary>
        /// プレイヤーを追従するカメラです。
        /// </summary>
        [Header("Camera")]
        [SerializeField]
        private Camera _playerCamera;

        /// <summary>
        /// プレイヤー入力を扱う Input System のラッパーです。
        /// </summary>
        [Header("Input")]
        [SerializeField]
        private GameInput _input;

        /// <summary>
        /// プレイヤー入力イベントを公開するコントローラーです。
        /// </summary>
        [SerializeField]
        private PlayerController _playerController;

        /// <summary>
        /// Bot入力イベントを公開するコントローラー群です。
        /// </summary>
        [Header("Bot")]
        [SerializeField]
        private BotController[] _botControllers;

        /// <summary>
        /// このクライアントから可視とみなすチームIDです。
        /// </summary>
        [Header("Visibility")]
        [SerializeField]
        private TeamId _visibleTeamId = TeamId.Primary;

        /// <summary>
        /// 選択ロールが不正な場合に使用するプレイヤーのフォールバックロールです。
        /// </summary>
        [Header("Fallback")]
        [SerializeField]
        private RoleType _fallbackPlayerRoleType = RoleType.Attacker;

        /// <summary>
        /// ロールとキャラクターPrefabの対応表です。
        /// </summary>
        [Header("Character Prefabs")]
        [SerializeField]
        private RoleCharacterPrefabEntry[] _roleCharacterPrefabEntries;

        /// <summary>
        /// スポナー検索対象を制限するレイヤーマスクです。0の場合はレイヤー制限を行いません。
        /// </summary>
        [Header("Spawn")]
        [Tooltip("スポナーを検索する対象レイヤー")]
        [SerializeField]
        private LayerMask _spawnerLayerMask;

        /// <summary>
        /// 生成済みキャラクターの実行時情報一覧です。
        /// </summary>
        private readonly List<CharacterRuntimeEntry> _characterEntries = new();

        /// <summary>
        /// GameObject から実行時情報を高速に取得するための辞書です。
        /// </summary>
        private readonly Dictionary<GameObject, CharacterRuntimeEntry>
            _characterEntryByObject = new();

        /// <summary>
        /// Botキャラクターが現在回復行動中かどうかを管理する辞書です。
        /// </summary>
        private readonly Dictionary<GameObject, bool>
            _isBotRecoveringByCharacterObject = new();

        /// <summary>
        /// キャラクターが最後に使用したスポナーを保持する辞書です。
        /// 復活位置の決定に使用します。
        /// </summary>
        private readonly Dictionary<GameObject, Component>
            _spawnerByCharacterObject = new();

        /// <summary>
        /// R3購読の破棄に使用する DisposableBag です。
        /// </summary>
        private DisposableBag _disposables;

        /// <summary>
        /// バトル開始時にキャラクター生成、カメラ設定、入力購読を初期化します。
        /// </summary>
        /// <returns>初期化を1フレーム遅延するためのコルーチンです。</returns>
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

            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

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

        /// <summary>
        /// 毎フレーム、キャラクターの草むらRevealと描画可否を更新します。
        /// </summary>
        private void Update()
        {
            UpdateCharacterVisibility();
        }

        /// <summary>
        /// プレイヤーチームのキャラクターを生成します。
        /// </summary>
        /// <param name="roleTypes">生成候補となるロール配列です。</param>
        /// <param name="selectedIndex">先頭プレイヤーに使用するロールのインデックスです。</param>
        /// <returns>操作対象となる先頭プレイヤーの GameObject です。</returns>
        /// <exception cref="InvalidOperationException">操作対象が生成されなかった場合に送出されます。</exception>
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

        /// <summary>
        /// 敵チームのキャラクターを生成します。
        /// </summary>
        /// <param name="roleTypes">生成候補となるロール配列です。</param>
        /// <param name="selectedIndex">プレイヤー側ロール選択の基準インデックスです。</param>
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

        /// <summary>
        /// 指定ロールとチームでキャラクターを生成し、スポナー配置、ステータス初期化、死亡購読を行います。
        /// </summary>
        /// <param name="roleType">生成するキャラクターのロールです。</param>
        /// <param name="teamId">所属チームです。</param>
        /// <returns>生成されたキャラクターの GameObject です。</returns>
        private GameObject SpawnCharacter(
            RoleType roleType,
            TeamId teamId)
        {
            GameObject prefab =
                FindCharacterPrefab(roleType);

            GameObject obj =
                Instantiate(prefab);

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

        /// <summary>
        /// キャラクターの実行時参照情報を作成します。
        /// </summary>
        /// <param name="characterObject">対象キャラクターの GameObject です。</param>
        /// <returns>作成された実行時情報です。</returns>
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
                    .GetComponentsInChildren<Renderer>(
                        true));
        }

        /// <summary>
        /// キャラクター GameObject に対応する実行時情報を取得します。
        /// </summary>
        /// <param name="characterObject">対象キャラクターの GameObject です。</param>
        /// <returns>対象キャラクターの実行時情報です。</returns>
        /// <exception cref="InvalidOperationException">実行時情報が存在しない場合に送出されます。</exception>
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

        /// <summary>
        /// キャラクターの死亡イベントを購読し、非表示化と復活処理を開始します。
        /// </summary>
        /// <param name="entry">購読対象キャラクターの実行時情報です。</param>
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

        /// <summary>
        /// 指定時間後にキャラクターを最後に使用したスポナー位置へ復活させます。
        /// </summary>
        /// <param name="status">復活対象のステータスです。</param>
        /// <returns>復活待機用のコルーチンです。</returns>
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

        /// <summary>
        /// プレイヤー以外の生成済みキャラクターに BotController を割り当てます。
        /// </summary>
        /// <param name="playerObject">プレイヤーが操作するキャラクターです。</param>
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

        /// <summary>
        /// 1体の BotController とキャラクターを接続し、移動・エイム・攻撃・回復行動を購読します。
        /// </summary>
        /// <param name="botController">入力イベント元の BotController です。</param>
        /// <param name="entry">Bot が操作するキャラクターの実行時情報です。</param>
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

                    characterAttack.Attack(
                        latestAimDirectionWorld);
                })
                .AddTo(ref _disposables);
        }

        /// <summary>
        /// Botが回復行動中に敵から離れるためのワールド方向を求めます。
        /// </summary>
        /// <param name="selfTransform">Botキャラクターの Transform です。</param>
        /// <returns>逃走に使う正規化済みワールド方向です。</returns>
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

        /// <summary>
        /// プレイヤーコントローラーの入力イベントをキャラクター操作へ接続します。
        /// </summary>
        /// <param name="playerObject">プレイヤーが操作するキャラクターです。</param>
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

        /// <summary>
        /// 可視チームのキャラクターだけが草むらをRevealできるように設定します。
        /// </summary>
        /// <param name="visibleTeamId">可視判定の基準となるチームIDです。</param>
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

        /// <summary>
        /// Reveal有効状態が前回値と異なる場合だけ CharacterReveal へ反映します。
        /// </summary>
        /// <param name="entry">対象キャラクターの実行時情報です。</param>
        /// <param name="isEnabled">Revealを有効にするかどうかです。</param>
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

        /// <summary>
        /// 草むらReveal状態とキャラクター描画状態を更新します。
        /// </summary>
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

                bool shouldRender =
                    ShouldRenderCharacter(
                        entry);

                SetCharacterRenderersEnabled(
                    entry,
                    shouldRender);
            }
        }

        /// <summary>
        /// 指定キャラクターを現在の可視チーム視点で描画すべきか判定します。
        /// </summary>
        /// <param name="targetEntry">描画判定対象の実行時情報です。</param>
        /// <returns>描画すべきなら true、隠すべきなら false です。</returns>
        private bool ShouldRenderCharacter(
            CharacterRuntimeEntry targetEntry)
        {
            CharacterStatus targetStatus =
                targetEntry.Status;

            return
                targetStatus.TeamId == _visibleTeamId
                || !targetStatus.IsInsideBush
                || IsInsideVisibleTeamRevealRange(
                    targetEntry);
        }

        /// <summary>
        /// 対象キャラクターが可視チームのReveal範囲内にいるか判定します。
        /// </summary>
        /// <param name="targetEntry">判定対象キャラクターの実行時情報です。</param>
        /// <returns>Reveal範囲内なら true です。</returns>
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

        /// <summary>
        /// キャラクター配下の Renderer を再取得し、実行時情報のキャッシュを更新します。
        /// </summary>
        /// <param name="characterObject">対象キャラクターの GameObject です。</param>
        /// <returns>取得した Renderer 配列です。</returns>
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

        /// <summary>
        /// キャラクターの Renderer 有効状態をまとめて切り替えます。
        /// </summary>
        /// <param name="entry">対象キャラクターの実行時情報です。</param>
        /// <param name="isEnabled">Rendererを有効にするかどうかです。</param>
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

        /// <summary>
        /// Renderer 配列の enabled を必要な場合だけ切り替えます。
        /// </summary>
        /// <param name="renderers">切り替え対象の Renderer 配列です。</param>
        /// <param name="isEnabled">Rendererを有効にするかどうかです。</param>
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

        /// <summary>
        /// キャラクターに対応する未使用スポナーを予約し、その位置と回転をキャラクターへ反映します。
        /// </summary>
        /// <param name="characterObject">配置対象のキャラクターです。</param>
        /// <param name="teamId">必要なスポナーのチームIDです。</param>
        /// <returns>予約した Spawner コンポーネントです。</returns>
        /// <exception cref="ArgumentNullException">characterObject が null の場合に送出されます。</exception>
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

        /// <summary>
        /// 指定チーム用の未使用スポナーを検索し、予約します。
        /// </summary>
        /// <param name="teamId">検索するスポナーのチームIDです。</param>
        /// <returns>予約できた Spawner コンポーネントです。</returns>
        /// <exception cref="InvalidOperationException">利用可能な Spawner が見つからない場合に送出されます。</exception>
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

        /// <summary>
        /// Spawner 型、TeamId プロパティ、TryReserve メソッドの Reflection 情報をキャッシュします。
        /// </summary>
        /// <exception cref="InvalidOperationException">必要な型またはメンバーが見つからない場合に送出されます。</exception>
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

        /// <summary>
        /// 現在読み込まれている Assembly から完全修飾名に一致する型を検索します。
        /// </summary>
        /// <param name="fullName">検索対象の完全修飾型名です。</param>
        /// <returns>見つかった型です。見つからない場合は null です。</returns>
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

        /// <summary>
        /// ロールに対応するキャラクターPrefabを取得します。
        /// </summary>
        /// <param name="roleType">検索するロールです。</param>
        /// <returns>対応するキャラクターPrefabです。</returns>
        /// <exception cref="InvalidOperationException">指定ロールのPrefabが未登録の場合に送出されます。</exception>
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

        /// <summary>
        /// 指定 GameObject から必須コンポーネントを取得します。
        /// </summary>
        /// <typeparam name="T">取得するコンポーネント型です。</typeparam>
        /// <param name="target">取得対象の GameObject です。</param>
        /// <returns>取得したコンポーネントです。</returns>
        /// <exception cref="InvalidOperationException">対象コンポーネントが存在しない場合に送出されます。</exception>
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

        /// <summary>
        /// R3購読と実行時キャッシュを破棄します。
        /// </summary>
        private void OnDestroy()
        {
            _disposables.Dispose();

            _characterEntries.Clear();
            _characterEntryByObject.Clear();
            _isBotRecoveringByCharacterObject.Clear();
            _spawnerByCharacterObject.Clear();
        }

        /// <summary>
        /// Bot視点で攻撃対象にできる最も近い敵キャラクターを検索します。
        /// </summary>
        /// <param name="selfTransform">検索を行うBot自身の Transform です。</param>
        /// <param name="searchRadius">検索半径です。</param>
        /// <returns>最も近い敵の GameObject です。見つからない場合は null です。</returns>
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

        /// <summary>
        /// プレイヤーの通常攻撃方向を、エイム結果とオートエイム設定から解決します。
        /// </summary>
        /// <param name="playerObject">プレイヤーキャラクターです。</param>
        /// <param name="aim">通常攻撃エイムコンポーネントです。</param>
        /// <param name="releasedDirectionFallback">リリース時に記録したフォールバック方向です。</param>
        /// <param name="latestAimDirectionWorld">最後に有効だったエイム方向です。</param>
        /// <returns>最終的に攻撃へ渡すワールド方向です。</returns>
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

        /// <summary>
        /// プレイヤーの必殺攻撃方向を、エイム結果とオートエイム設定から解決します。
        /// </summary>
        /// <param name="playerObject">プレイヤーキャラクターです。</param>
        /// <param name="aim">必殺攻撃エイムコンポーネントです。</param>
        /// <param name="releasedDirectionFallback">リリース時に記録したフォールバック方向です。</param>
        /// <param name="latestAimDirectionWorld">最後に有効だったエイム方向です。</param>
        /// <returns>最終的に必殺攻撃へ渡すワールド方向です。</returns>
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

        /// <summary>
        /// 基準方向とオートエイム可否から、実際に使用する攻撃方向を解決します。
        /// </summary>
        /// <param name="playerObject">プレイヤーキャラクターです。</param>
        /// <param name="baseDirectionWorld">入力またはエイムから得た基準方向です。</param>
        /// <param name="canAutoAim">オートエイムを許可するかどうかです。</param>
        /// <returns>正規化済みの攻撃方向です。</returns>
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

        /// <summary>
        /// 指定キャラクターの前方円錐範囲内にいる最も近い敵を検索します。
        /// </summary>
        /// <param name="selfTransform">検索者の Transform です。</param>
        /// <param name="forward">検索者の正規化済み前方方向です。</param>
        /// <param name="searchRadius">検索半径です。</param>
        /// <param name="maxAngleDegrees">許容する最大角度です。</param>
        /// <returns>最も近い敵の GameObject です。見つからない場合は null です。</returns>
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

        /// <summary>
        /// ロールとキャラクターPrefabの対応を保持する設定用エントリです。
        /// </summary>
        [Serializable]
        private sealed class RoleCharacterPrefabEntry
        {
            /// <summary>
            /// 対応するロールです。
            /// </summary>
            [SerializeField]
            private RoleType _roleType;

            /// <summary>
            /// 対応するキャラクターPrefabです。
            /// </summary>
            [SerializeField]
            private GameObject _characterPrefab;

            /// <summary>
            /// 対応するロールを取得します。
            /// </summary>
            public RoleType RoleType =>
                _roleType;

            /// <summary>
            /// 対応するキャラクターPrefabを取得します。
            /// </summary>
            public GameObject CharacterPrefab =>
                _characterPrefab;
        }

        /// <summary>
        /// 生成済みキャラクターに必要な実行時参照をまとめた内部データです。
        /// </summary>
        private sealed class CharacterRuntimeEntry
        {
            /// <summary>
            /// 実行時参照を初期化します。
            /// </summary>
            /// <param name="gameObject">キャラクターの GameObject です。</param>
            /// <param name="transform">キャラクターの Transform です。</param>
            /// <param name="status">キャラクターのステータスです。</param>
            /// <param name="reveal">キャラクターのReveal範囲です。</param>
            /// <param name="renderers">キャラクター配下の Renderer 配列です。</param>
            public CharacterRuntimeEntry(
                GameObject gameObject,
                Transform transform,
                CharacterStatus status,
                CharacterReveal reveal,
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

                Renderers =
                    renderers;
            }

            /// <summary>
            /// キャラクターの GameObject です。
            /// </summary>
            public GameObject GameObject
            {
                get;
            }

            /// <summary>
            /// キャラクターの Transform です。
            /// </summary>
            public Transform Transform
            {
                get;
            }

            /// <summary>
            /// キャラクターのステータスです。
            /// </summary>
            public CharacterStatus Status
            {
                get;
            }

            /// <summary>
            /// キャラクターのReveal範囲です。
            /// </summary>
            public CharacterReveal Reveal
            {
                get;
            }

            /// <summary>
            /// キャラクター配下の Renderer 配列です。
            /// </summary>
            public Renderer[] Renderers
            {
                get;
                set;
            }

            /// <summary>
            /// Reveal有効状態の前回値を保持しているかどうかです。
            /// </summary>
            public bool HasRevealEnabledCache
            {
                get;
                set;
            }

            /// <summary>
            /// 最後に CharacterReveal へ反映した有効状態です。
            /// </summary>
            public bool LastRevealEnabled
            {
                get;
                set;
            }
        }
    }
}
