using System.Collections;

using UnityEngine;

using Uraty.Shared.Battle;

namespace Uraty.Features.Player
{
    /// <summary>
    /// プレイヤーの攻撃要求を処理し、
    /// RoleDefinition を参照して弾生成要求を送るクラス。
    ///
    /// このクラスは入力を直接読まない。
    /// 入力解釈は PlayerInputInterpreter、
    /// Aim 状態・プレビュー・発射要求の管理は PlayerAim、
    /// 実際の攻撃実行は PlayerCombat が担当する。
    ///
    /// 実際の弾生成や移動は IBulletSpawner 実装側に委譲する。
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("References")]
        [SerializeField] private PlayerStatus _playerStatus;
        [SerializeField] private PlayerAim _playerAim;

        [Header("Spawn")]
        [SerializeField] private float _spawnHeightOffset = 0.5f;

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour _bulletSpawnerBehaviour;

        private IBulletSpawner _bulletSpawner;

        private float _nextAttackTime;
        private float _nextSuperTime;

        private void Awake()
        {
            ResolveReferences();
            ResolveBulletSpawner();
        }

        private void Start()
        {
            if (_bulletSpawner != null)
            {
                return;
            }

            _bulletSpawner = ResolveBulletSpawnerFromComponentsInChildren();
        }

        private void Reset()
        {
            _playerStatus = GetComponent<PlayerStatus>();
            _playerAim = GetComponent<PlayerAim>();
        }

        private void LateUpdate()
        {
            if (!CanProcessCombat())
            {
                return;
            }

            TryHandleAttack();
            TryHandleSuper();
        }

        private void ResolveReferences()
        {
            if (_playerStatus == null)
            {
                _playerStatus = GetComponent<PlayerStatus>();
            }

            if (_playerAim == null)
            {
                _playerAim = GetComponent<PlayerAim>();
            }
        }

        private void ResolveBulletSpawner()
        {
            _bulletSpawner = ResolveBulletSpawnerFromBehaviour(_bulletSpawnerBehaviour);

            if (_bulletSpawner != null)
            {
                return;
            }

            _bulletSpawner = ResolveBulletSpawnerFromComponents(gameObject);

            if (_bulletSpawner != null)
            {
                return;
            }

            _bulletSpawner = ResolveBulletSpawnerFromComponentsInParent();
        }

        private bool CanProcessCombat()
        {
            if (_bulletSpawner == null)
            {
                return false;
            }

            if (_playerAim == null)
            {
                return false;
            }

            if (_playerStatus == null)
            {
                return false;
            }

            return _playerStatus.RoleDefinition != null;
        }

        private void TryHandleAttack()
        {
            if (!_playerAim.TryConsumeAttack(
                    out Vector3 aimPoint,
                    out Vector3 aimDirection,
                    out bool canAutoAim))
            {
                return;
            }

            AttackDefinition attackDefinition = _playerStatus.RoleDefinition.Attack;
            if (attackDefinition == null)
            {
                return;
            }

            if (Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime = Time.time + GetAttackIntervalSeconds(attackDefinition);

            ExecuteDefinition(
                attackDefinition,
                aimPoint,
                aimDirection,
                canAutoAim);
        }

        private void TryHandleSuper()
        {
            if (!_playerAim.TryConsumeSuper(
                    out Vector3 aimPoint,
                    out Vector3 aimDirection,
                    out bool canAutoAim))
            {
                return;
            }

            AttackDefinition superDefinition = _playerStatus.RoleDefinition.Super;
            if (superDefinition == null)
            {
                return;
            }

            if (Time.time < _nextSuperTime)
            {
                return;
            }

            _nextSuperTime = Time.time + GetAttackIntervalSeconds(superDefinition);

            ExecuteDefinition(
                superDefinition,
                aimPoint,
                aimDirection,
                canAutoAim);
        }

        private float GetAttackIntervalSeconds(AttackDefinition definition)
        {
            if (definition == null)
            {
                return 0f;
            }

            return Mathf.Max(
                definition.MinAttackIntervalSeconds,
                definition.MaxAttackIntervalSeconds);
        }

        private void ExecuteDefinition(
            AttackDefinition definition,
            Vector3 aimPoint,
            Vector3 aimDirection,
            bool canAutoAim)
        {
            Vector3 resolvedAimDirection = ResolveAimDirection(aimDirection);

            // 現時点では AutoAim の対象探索は未実装。
            // canAutoAim は PlayerInputInterpreter / PlayerAim から正しく届いているが、
            // ここで最近傍敵などを探すシステムが無いので、今は方向補正には使わない。
            // 後で TargetResolver / EnemySearchSystem を追加したらここで差し替える。
            _ = canAutoAim;

            switch (definition.Type)
            {
                case AimType.Line:
                    FireLine(definition, resolvedAimDirection);
                    break;

                case AimType.Fan:
                    FireFan(definition, resolvedAimDirection);
                    break;

                case AimType.Throw:
                    FireThrow(definition, aimPoint, resolvedAimDirection);
                    break;
            }
        }

        private void FireLine(AttackDefinition attackDefinition, Vector3 aimDirection)
        {
            LineAttackDefinition lineDefinition = attackDefinition.Line;
            if (lineDefinition == null)
            {
                return;
            }

            LineBulletDefinition[] bullets = lineDefinition.Bullets;
            if (bullets == null || bullets.Length == 0)
            {
                return;
            }

            for (int bulletIndex = 0; bulletIndex < bullets.Length; bulletIndex++)
            {
                LineBulletDefinition bulletDefinition = bullets[bulletIndex];
                if (bulletDefinition == null || bulletDefinition.BulletPrefab == null)
                {
                    continue;
                }

                LineAimLineDefinition aimLineDefinition =
                    GetAssignedAimLine(lineDefinition.AimLines, bulletIndex);

                if (aimLineDefinition == null)
                {
                    continue;
                }

                StartCoroutine(SpawnLineBulletDelayed(
                    attackDefinition,
                    lineDefinition,
                    aimLineDefinition,
                    bulletDefinition,
                    aimDirection));
            }
        }

        private IEnumerator SpawnLineBulletDelayed(
            AttackDefinition attackDefinition,
            LineAttackDefinition lineDefinition,
            LineAimLineDefinition aimLineDefinition,
            LineBulletDefinition bulletDefinition,
            Vector3 aimDirection)
        {
            float spawnDelaySeconds = Mathf.Max(0f, bulletDefinition.SpawnDelaySeconds);
            if (spawnDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(spawnDelaySeconds);
            }

            SpawnLineBullet(
                attackDefinition,
                lineDefinition,
                aimLineDefinition,
                bulletDefinition,
                aimDirection);
        }

        private void SpawnLineBullet(
            AttackDefinition attackDefinition,
            LineAttackDefinition lineDefinition,
            LineAimLineDefinition aimLineDefinition,
            LineBulletDefinition bulletDefinition,
            Vector3 aimDirection)
        {
            Vector3 baseDirection = ResolveAimDirection(aimDirection);

            Vector3 lineDirection =
                Quaternion.Euler(0f, aimLineDefinition.OffsetAngleFromAimLine, 0f) * baseDirection;

            lineDirection = ResolveAimDirection(lineDirection);

            Vector3 rightDirection = GetRight(lineDirection);
            float lateralOffsetMeters =
                aimLineDefinition.OffsetDistanceFromAimLine + bulletDefinition.OffsetFromAimLine;

            Vector3 spawnOrigin = GetSpawnOrigin();

            Vector3 bulletLocalOffset = TransformLocalSpawnOffset(
                lineDirection,
                bulletDefinition.SpawnOffsetFromPlayerCenter);

            Vector3 spawnPosition =
                spawnOrigin
                + bulletLocalOffset
                + (rightDirection * lateralOffsetMeters);

            _bulletSpawner.SpawnLineBullet(
                bulletPrefab: bulletDefinition.BulletPrefab,
                ownerTransform: transform,
                ownerTeamId: _playerStatus.TeamId,
                startPosition: spawnPosition,
                direction: lineDirection,
                maxTravelDistanceMeters: Mathf.Max(0f, aimLineDefinition.EffectiveRange),
                definition: lineDefinition,
                attackDefinition: attackDefinition);
        }

        private void FireFan(AttackDefinition attackDefinition, Vector3 aimDirection)
        {
            FanAttackDefinition fanDefinition = attackDefinition.Fan;
            if (fanDefinition == null)
            {
                return;
            }

            FanBulletDefinition[] bullets = fanDefinition.Bullets;
            if (bullets == null || bullets.Length == 0)
            {
                return;
            }

            for (int bulletIndex = 0; bulletIndex < bullets.Length; bulletIndex++)
            {
                FanBulletDefinition bulletDefinition = bullets[bulletIndex];
                if (bulletDefinition == null || bulletDefinition.BulletPrefab == null)
                {
                    continue;
                }

                StartCoroutine(SpawnFanBulletDelayed(
                    attackDefinition,
                    fanDefinition,
                    bulletDefinition,
                    aimDirection));
            }
        }

        private IEnumerator SpawnFanBulletDelayed(
            AttackDefinition attackDefinition,
            FanAttackDefinition fanDefinition,
            FanBulletDefinition bulletDefinition,
            Vector3 aimDirection)
        {
            float spawnDelaySeconds = Mathf.Max(0f, bulletDefinition.SpawnDelaySeconds);
            if (spawnDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(spawnDelaySeconds);
            }

            SpawnFanBullet(
                attackDefinition,
                fanDefinition,
                bulletDefinition,
                aimDirection);
        }

        private void SpawnFanBullet(
            AttackDefinition attackDefinition,
            FanAttackDefinition fanDefinition,
            FanBulletDefinition bulletDefinition,
            Vector3 aimDirection)
        {
            Vector3 baseDirection = ResolveAimDirection(aimDirection);

            Vector3 bulletDirection =
                Quaternion.Euler(0f, bulletDefinition.OffsetAngleFromCenter, 0f) * baseDirection;

            bulletDirection = ResolveAimDirection(bulletDirection);

            Vector3 spawnOrigin = GetSpawnOrigin();
            Vector3 bulletLocalOffset = TransformLocalSpawnOffset(
                bulletDirection,
                bulletDefinition.SpawnOffsetFromPlayerCenter);

            Vector3 spawnPosition = spawnOrigin + bulletLocalOffset;

            _bulletSpawner.SpawnFanBullet(
                bulletPrefab: bulletDefinition.BulletPrefab,
                ownerTransform: transform,
                ownerTeamId: _playerStatus.TeamId,
                startPosition: spawnPosition,
                direction: bulletDirection,
                maxTravelDistanceMeters: Mathf.Max(0f, fanDefinition.RangeMeters),
                definition: fanDefinition,
                attackDefinition: attackDefinition);
        }

        private void FireThrow(
            AttackDefinition attackDefinition,
            Vector3 aimPoint,
            Vector3 aimDirection)
        {
            // Throw は別段階で対応。
            _ = attackDefinition;
            _ = aimPoint;
            _ = aimDirection;
        }

        private Vector3 GetSpawnOrigin()
        {
            return transform.position + (Vector3.up * _spawnHeightOffset);
        }

        private static IBulletSpawner ResolveBulletSpawnerFromBehaviour(MonoBehaviour behaviour)
        {
            return behaviour as IBulletSpawner;
        }

        private static IBulletSpawner ResolveBulletSpawnerFromComponents(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = targetObject.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IBulletSpawner bulletSpawner)
                {
                    return bulletSpawner;
                }
            }

            return null;
        }

        private IBulletSpawner ResolveBulletSpawnerFromComponentsInParent()
        {
            Transform current = transform.parent;
            while (current != null)
            {
                IBulletSpawner bulletSpawner = ResolveBulletSpawnerFromComponents(current.gameObject);
                if (bulletSpawner != null)
                {
                    return bulletSpawner;
                }

                current = current.parent;
            }

            return null;
        }

        private IBulletSpawner ResolveBulletSpawnerFromComponentsInChildren()
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IBulletSpawner bulletSpawner)
                {
                    return bulletSpawner;
                }
            }

            return null;
        }

        private Vector3 ResolveAimDirection(Vector3 aimDirection)
        {
            Vector3 direction = aimDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = Vector3.forward;
            }

            return direction.normalized;
        }

        private Vector3 GetRight(Vector3 forwardDirection)
        {
            Vector3 forward = ResolveAimDirection(forwardDirection);
            return Quaternion.Euler(0f, 90f, 0f) * forward;
        }

        private static T GetAssignedAimLine<T>(T[] definitions, int index) where T : class
        {
            if (definitions == null || definitions.Length == 0)
            {
                return null;
            }

            if (index < 0 || index >= definitions.Length)
            {
                return definitions[definitions.Length - 1];
            }

            if (definitions[index] != null)
            {
                return definitions[index];
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        private Vector3 TransformLocalSpawnOffset(Vector3 forwardDirection, Vector3 localOffset)
        {
            Vector3 normalizedForward = ResolveAimDirection(forwardDirection);
            Vector3 rightDirection = GetRight(normalizedForward);

            return (rightDirection * localOffset.x)
                + (Vector3.up * localOffset.y)
                + (normalizedForward * localOffset.z);
        }
    }
}
