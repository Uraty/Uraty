using System;
using System.Collections.Generic;

using R3;

using UnityEngine;

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

        [Header("Visibility")]
        [SerializeField]
        private TeamId _visibleTeamId = TeamId.Primary;

        [Header("Fallback")]
        [SerializeField]
        private RoleType _fallbackPlayerRoleType = RoleType.Attacker;

        [Header("Character Prefabs")]
        [SerializeField]
        private RoleCharacterPrefabEntry[] _roleCharacterPrefabEntries;

        private readonly List<GameObject> _characterObjects = new();
        private readonly Dictionary<GameObject, Renderer[]> _characterRenderersByObject = new();

        private DisposableBag _disposables;

        private void Start()
        {
            _input.Player.Enable();

            RoleType[] roleTypes = (RoleType[])Enum.GetValues(typeof(RoleType));
            int selectedIndex = Array.IndexOf(roleTypes, _fallbackPlayerRoleType);

            GameObject playerObject = SpawnPlayerTeam(roleTypes, selectedIndex);

            SpawnEnemyTeam(roleTypes, selectedIndex);

            ConfigureBushRevealSensors(_visibleTeamId);

            _playerCamera.GetComponent<CameraMove>().SetTarget(playerObject);

            SubscribePlayerController(playerObject);
        }

        private void Update()
        {
            UpdateCharacterVisibility();
        }

        private GameObject SpawnPlayerTeam(RoleType[] roleTypes, int selectedIndex)
        {
            GameObject playerObject = null;

            for (int i = 0; i < TeamMemberCount; i++)
            {
                RoleType roleType =
                    roleTypes[(selectedIndex + i) % roleTypes.Length];

                GameObject characterObject = SpawnCharacter(
                    roleType,
                    TeamId.Primary);

                if (i == 0)
                {
                    playerObject = characterObject;
                }
            }

            if (playerObject == null)
            {
                throw new InvalidOperationException(
                    "操作対象の Character が生成されませんでした。");
            }

            return playerObject;
        }

        private void SpawnEnemyTeam(RoleType[] roleTypes, int selectedIndex)
        {
            for (int i = 0; i < TeamMemberCount; i++)
            {
                RoleType roleType =
                    roleTypes[(selectedIndex + TeamMemberCount + i) % roleTypes.Length];

                SpawnCharacter(
                    roleType,
                    TeamId.Secondary);
            }
        }

        private GameObject SpawnCharacter(RoleType roleType, TeamId teamId)
        {
            GameObject characterPrefab = FindCharacterPrefab(roleType);
            GameObject characterObject = Instantiate(characterPrefab);

            CharacterStatus characterStatus = GetRequiredComponent<CharacterStatus>(characterObject);
            characterStatus.Initialize(teamId);

            _characterObjects.Add(characterObject);
            CacheCharacterRenderers(characterObject);

            return characterObject;
        }

        private void ConfigureBushRevealSensors(TeamId visibleTeamId)
        {
            for (int i = 0; i < _characterObjects.Count; i++)
            {
                GameObject characterObject = _characterObjects[i];

                if (characterObject == null)
                {
                    continue;
                }

                CharacterStatus characterStatus =
                    GetRequiredComponent<CharacterStatus>(characterObject);

                CharacterReveal revealSensor =
                    GetRequiredComponent<CharacterReveal>(characterObject);

                bool shouldRevealBush =
                    characterStatus.TeamId == visibleTeamId
                    && !characterStatus.IsDead;

                revealSensor.SetRevealEnabled(shouldRevealBush);
            }
        }

        private void UpdateCharacterVisibility()
        {
            ConfigureBushRevealSensors(_visibleTeamId);

            for (int i = 0; i < _characterObjects.Count; i++)
            {
                GameObject targetObject = _characterObjects[i];

                if (targetObject == null)
                {
                    continue;
                }

                if (!targetObject.activeInHierarchy)
                {
                    continue;
                }

                bool shouldRender = ShouldRenderCharacter(targetObject);

                SetCharacterRenderersEnabled(
                    targetObject,
                    shouldRender);
            }
        }

        private bool ShouldRenderCharacter(GameObject targetObject)
        {
            CharacterStatus targetStatus =
                GetRequiredComponent<CharacterStatus>(targetObject);

            return
                targetStatus.TeamId == _visibleTeamId
                || !targetStatus.IsInsideBush
                || IsInsideVisibleTeamRevealRange(targetObject);
        }

        private bool IsInsideVisibleTeamRevealRange(GameObject targetObject)
        {
            Vector3 targetPosition = targetObject.transform.position;

            for (int i = 0; i < _characterObjects.Count; i++)
            {
                GameObject viewerObject = _characterObjects[i];

                if (viewerObject == null || viewerObject == targetObject)
                {
                    continue;
                }

                if (!viewerObject.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus viewerStatus =
                    GetRequiredComponent<CharacterStatus>(viewerObject);

                if (viewerStatus.TeamId != _visibleTeamId)
                {
                    continue;
                }

                if (viewerStatus.IsDead)
                {
                    continue;
                }

                CharacterReveal viewerReveal =
                    GetRequiredComponent<CharacterReveal>(viewerObject);

                if (viewerReveal.ContainsWorldPosition(targetPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheCharacterRenderers(GameObject characterObject)
        {
            Renderer[] renderers =
                characterObject.GetComponentsInChildren<Renderer>(true);

            _characterRenderersByObject[characterObject] = renderers;
        }

        private void SetCharacterRenderersEnabled(
            GameObject characterObject,
            bool isEnabled)
        {
            if (!_characterRenderersByObject.TryGetValue(
                    characterObject,
                    out Renderer[] renderers))
            {
                CacheCharacterRenderers(characterObject);
                renderers = _characterRenderersByObject[characterObject];
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (renderer.enabled == isEnabled)
                {
                    continue;
                }

                renderer.enabled = isEnabled;
            }
        }

        private void SubscribePlayerController(GameObject playerObject)
        {
            CharacterMove characterMove = GetRequiredComponent<CharacterMove>(playerObject);
            CharacterAttackAim characterAttackAim = GetRequiredComponent<CharacterAttackAim>(playerObject);
            CharacterSuperAim characterSuperAim = GetRequiredComponent<CharacterSuperAim>(playerObject);
            CharacterAttack characterAttack = GetRequiredComponent<CharacterAttack>(playerObject);
            CharacterSuper characterSuper = GetRequiredComponent<CharacterSuper>(playerObject);

            Vector3 latestAimDirectionWorld = Vector3.forward;

            _playerController.MoveRequestedStream
                .Subscribe(request =>
                {
                    characterMove.Move(request.MoveDirectionWorld);
                })
                .AddTo(ref _disposables);

            _playerController.AimRequestedStream
                .Subscribe(request =>
                {
                    latestAimDirectionWorld = request.AimDirectionWorld;

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
                    if (request.PressedThisFrame)
                    {
                        characterAttackAim.BeginAttackAim();
                    }

                    if (request.ReleasedThisFrame)
                    {
                        characterAttackAim.CompleteAttackAim();
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
                    }
                })
                .AddTo(ref _disposables);

            _playerController.AttackRequestedStream
                .Subscribe(request =>
                {
                    Vector3 attackDirection = latestAimDirectionWorld;

                    if (characterAttackAim.TryConsumeAttack(
                            out Vector3 aimPoint,
                            out Vector3 targetDirection,
                            out bool canAutoAim))
                    {
                        if (canAutoAim)
                        {
                            if (!TryResolveAutoAimDirection(
                                    playerObject,
                                    out attackDirection))
                            {
                                attackDirection = ResolveForwardDirection(playerObject);
                            }
                        }
                        else
                        {
                            attackDirection = targetDirection;
                        }
                    }

                    characterAttack.Attack(attackDirection);
                })
                .AddTo(ref _disposables);

            _playerController.SuperRequestedStream
                .Subscribe(request =>
                {
                    Vector3 superDirection = latestAimDirectionWorld;

                    if (characterSuperAim.TryConsumeSuper(
                            out Vector3 aimPoint,
                            out Vector3 targetDirection,
                            out bool canAutoAim))
                    {
                        if (canAutoAim)
                        {
                            if (!TryResolveAutoAimDirection(
                                    playerObject,
                                    out superDirection))
                            {
                                superDirection = ResolveForwardDirection(playerObject);
                            }
                        }
                        else
                        {
                            superDirection = targetDirection;
                        }
                    }

                    characterSuper.Super(superDirection);
                })
                .AddTo(ref _disposables);
        }

        private bool TryResolveAutoAimDirection(
            GameObject sourceObject,
            out Vector3 direction)
        {
            Vector3 sourcePosition = sourceObject.transform.position;
            CharacterStatus sourceStatus = GetRequiredComponent<CharacterStatus>(sourceObject);

            GameObject nearestObject = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < _characterObjects.Count; i++)
            {
                GameObject characterObject = _characterObjects[i];

                if (characterObject == null || characterObject == sourceObject)
                {
                    continue;
                }

                if (!characterObject.activeInHierarchy)
                {
                    continue;
                }

                CharacterStatus characterStatus =
                    GetRequiredComponent<CharacterStatus>(characterObject);

                if (characterStatus.TeamId == sourceStatus.TeamId)
                {
                    continue;
                }

                if (characterStatus.IsDead)
                {
                    continue;
                }

                if (!ShouldRenderCharacter(characterObject))
                {
                    continue;
                }

                if (!IsCharacterRendererInsidePlayerCamera(characterObject))
                {
                    continue;
                }

                Vector3 toTarget = characterObject.transform.position - sourcePosition;
                toTarget.y = 0.0f;

                float sqrDistance = toTarget.sqrMagnitude;

                if (sqrDistance <= MinDirectionSqrMagnitude)
                {
                    continue;
                }

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestObject = characterObject;
                }
            }

            if (nearestObject == null)
            {
                direction = Vector3.zero;
                return false;
            }

            Vector3 resolvedDirection = nearestObject.transform.position - sourcePosition;
            resolvedDirection.y = 0.0f;

            if (resolvedDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = Vector3.zero;
                return false;
            }

            direction = resolvedDirection.normalized;
            return true;
        }

        private bool IsCharacterRendererInsidePlayerCamera(GameObject targetObject)
        {
            if (_playerCamera == null)
            {
                return false;
            }

            if (!_characterRenderersByObject.TryGetValue(
                    targetObject,
                    out Renderer[] renderers))
            {
                CacheCharacterRenderers(targetObject);
                renderers = _characterRenderersByObject[targetObject];
            }

            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            Plane[] cameraPlanes =
                GeometryUtility.CalculateFrustumPlanes(_playerCamera);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (!renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (GeometryUtility.TestPlanesAABB(
                        cameraPlanes,
                        renderer.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ResolveForwardDirection(GameObject sourceObject)
        {
            Vector3 forward = sourceObject.transform.forward;
            forward.y = 0.0f;

            if (forward.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return Vector3.forward;
            }

            return forward.normalized;
        }

        private GameObject FindCharacterPrefab(RoleType roleType)
        {
            foreach (RoleCharacterPrefabEntry entry in _roleCharacterPrefabEntries)
            {
                if (entry.RoleType == roleType)
                {
                    return entry.CharacterPrefab;
                }
            }

            throw new InvalidOperationException(
                $"{roleType} に対応する CharacterPrefab が登録されていません。");
        }

        private static T GetRequiredComponent<T>(GameObject target)
            where T : Component
        {
            if (target.TryGetComponent(out T component))
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

            public RoleType RoleType => _roleType;
            public GameObject CharacterPrefab => _characterPrefab;
        }
    }
}
