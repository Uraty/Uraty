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
        private const int TeamMemberCount = 3;
        private const float MinDirectionSqrMagnitude = 0.0001f;

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

            inputInterpreter.Initialize(status);

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
                    characterAttack.Attack(
                        latestAimDirectionWorld);
                })
                .AddTo(ref _disposables);
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

            _playerController.AttackRequestedStream
                .Subscribe(_ =>
                {
                    characterAttack.Attack(
                        latestAimDirectionWorld);
                })
                .AddTo(ref _disposables);

            _playerController.SuperRequestedStream
                .Subscribe(_ =>
                {
                    characterSuper.Super(
                        latestAimDirectionWorld);
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

            if (spawners == null || spawners.Length ==0)
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

            for (int i =0;
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
                if (_spawnerLayerMask.value !=0)
                {
                    int spawnerLayerBit =1 << spawner.gameObject.layer;
                    bool isTargetLayer =
                        (_spawnerLayerMask.value & spawnerLayerBit) !=0;

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

            for (int i =0; i < assemblies.Length; i++)
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
