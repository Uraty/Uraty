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
        [SerializeField] private CameraMove _cameraMove;

        [Header("Input")]
        [SerializeField] private GameInput _input;
        [SerializeField] private PlayerController _playerController;

        [Header("Fallback")]
        [SerializeField] private RoleType _fallbackPlayerRoleType = RoleType.Attacker;

        [Header("Character Prefabs")]
        [SerializeField] private RoleCharacterPrefabEntry[] _roleCharacterPrefabEntries;

        private readonly List<GameObject> _characterObjects = new();

        private DisposableBag _disposables;

        private void Start()
        {
            _input.Player.Enable();

            RoleType[] roleTypes = (RoleType[])Enum.GetValues(typeof(RoleType));
            int selectedIndex = Array.IndexOf(roleTypes, _fallbackPlayerRoleType);

            GameObject playerObject = SpawnPlayerTeam(roleTypes, selectedIndex);

            SpawnEnemyTeam(roleTypes, selectedIndex);

            _cameraMove.SetTarget(playerObject);

            SubscribePlayerController(playerObject);
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

            return characterObject;
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
                        attackDirection = canAutoAim
                            ? ResolveNearestCharacterDirection(playerObject, targetDirection)
                            : targetDirection;
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
                        superDirection = canAutoAim
                            ? ResolveNearestCharacterDirection(playerObject, targetDirection)
                            : targetDirection;
                    }

                    characterSuper.Super(superDirection);
                })
                .AddTo(ref _disposables);
        }

        private Vector3 ResolveNearestCharacterDirection(
            GameObject sourceObject,
            Vector3 fallbackDirection)
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

                CharacterStatus characterStatus = GetRequiredComponent<CharacterStatus>(characterObject);
                if (characterStatus.IsSameTeam(sourceStatus.TeamId))
                {
                    continue;
                }

                Vector3 direction = characterObject.transform.position - sourcePosition;
                direction.y = 0f;

                float sqrDistance = direction.sqrMagnitude;
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
                return ResolveFallbackDirection(sourceObject, fallbackDirection);
            }

            Vector3 nearestDirection = nearestObject.transform.position - sourcePosition;
            nearestDirection.y = 0f;

            if (nearestDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return ResolveFallbackDirection(sourceObject, fallbackDirection);
            }

            return nearestDirection.normalized;
        }

        private Vector3 ResolveFallbackDirection(
            GameObject sourceObject,
            Vector3 fallbackDirection)
        {
            fallbackDirection.y = 0f;

            if (fallbackDirection.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                return fallbackDirection.normalized;
            }

            Vector3 forward = sourceObject.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                return forward.normalized;
            }

            return Vector3.forward;
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
        }

        [Serializable]
        private sealed class RoleCharacterPrefabEntry
        {
            [SerializeField] private RoleType _roleType;
            [SerializeField] private GameObject _characterPrefab;

            public RoleType RoleType => _roleType;
            public GameObject CharacterPrefab => _characterPrefab;
        }
    }
}
