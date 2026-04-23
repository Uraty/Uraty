using System.Collections;
using UnityEngine;
using Uraty.Shared.Battle;

namespace Uraty.Feature.Player
{
    /// <summary>
    /// プレイヤーの攻撃入力を受け取り、
    /// RoleDefinition を参照して弾生成要求を送るクラス。
    ///
    /// このクラスは「撃つ判断」と「発射パラメータの組み立て」までを担当する。
    /// 実際の弾生成や移動は IBulletSpawner 実装側に委譲する。
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private PlayerStatus _playerStatus;
        [SerializeField] private PlayerAim _playerAim;
        [SerializeField] private Transform _shotOrigin;

        [Header("Spawn")]
        // 弾の生成位置を少し上げたい場合の高さオフセット。
        [SerializeField] private float _spawnHeightOffset = 0.5f;

        [Header("Dependencies")]
        // Inspector から IBulletSpawner 実装を差し込むための参照。
        [SerializeField] private MonoBehaviour _bulletSpawnerBehaviour;

        private IBulletSpawner _bulletSpawner;

        // 通常攻撃の次回発射可能時刻。
        private float _nextAttackTime;

        // 必殺技の次回発射可能時刻。
        private float _nextSpecialTime;

        private void Awake()
        {
            // まずは Inspector 指定を優先して解決する。
            _bulletSpawner = ResolveBulletSpawnerFromBehaviour(_bulletSpawnerBehaviour);

            if (_bulletSpawner != null)
            {
                return;
            }

            // 同一オブジェクトから取得を試みる。
            _bulletSpawner = ResolveBulletSpawnerFromComponents(gameObject);

            if (_bulletSpawner != null)
            {
                return;
            }

            // 親方向も探索して補完する。
            _bulletSpawner = ResolveBulletSpawnerFromComponentsInParent();
        }

        private void Start()
        {
            if (_bulletSpawner != null)
            {
                return;
            }

            // 最後の補完として子階層も探索する。
            _bulletSpawner = ResolveBulletSpawnerFromComponentsInChildren();
        }

        /// <summary>
        /// コンポーネント追加時の初期参照補完。
        /// </summary>
        private void Reset()
        {
            _playerStatus = GetComponent<PlayerStatus>();
            _playerAim = GetComponent<PlayerAim>();
            _shotOrigin = transform;
        }

        /// <summary>
        /// Aim 側で更新された攻撃要求を回収する。
        /// </summary>
        private void LateUpdate()
        {
            if (_bulletSpawner == null)
            {
                return;
            }

            if (_playerAim == null)
            {
                return;
            }

            if (_playerStatus == null)
            {
                return;
            }

            if (_playerStatus.RoleDefinition == null)
            {
                return;
            }

            TryHandleAttack();
            TryHandleSpecial();
        }

        /// <summary>
        /// 通常攻撃要求を処理する。
        /// </summary>
        private void TryHandleAttack()
        {
            if (!_playerAim.TryConsumeAttack(out Vector3 aimPoint, out Vector3 aimDirection))
            {
                return;
            }

            AttackDefinition attackDefinition = _playerStatus.RoleDefinition.Attack;
            if (attackDefinition == null)
            {
                return;
            }

            // 攻撃間隔内なら発射しない。
            if (Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime = Time.time + Mathf.Max(
                attackDefinition.MinAttackIntervalSeconds,
                attackDefinition.MaxAttackIntervalSeconds);

            ExecuteDefinition(attackDefinition, aimPoint, aimDirection);
        }

        /// <summary>
        /// 必殺技要求を処理する。
        /// </summary>
        private void TryHandleSpecial()
        {
            if (!_playerAim.TryConsumeSpecial(out Vector3 aimPoint, out Vector3 aimDirection))
            {
                return;
            }

            AttackDefinition specialDefinition = _playerStatus.RoleDefinition.Special;
            if (specialDefinition == null)
            {
                return;
            }

            // 必殺技の発動間隔内なら発射しない。
            if (Time.time < _nextSpecialTime)
            {
                return;
            }

            _nextSpecialTime = Time.time + Mathf.Max(
                specialDefinition.MinAttackIntervalSeconds,
                specialDefinition.MaxAttackIntervalSeconds);

            ExecuteDefinition(specialDefinition, aimPoint, aimDirection);
        }

        /// <summary>
        /// 攻撃定義の AimType に応じて発射方法を振り分ける。
        /// </summary>
        private void ExecuteDefinition(
            AttackDefinition definition,
            Vector3 aimPoint,
            Vector3 aimDirection)
        {
            switch (definition.Type)
            {
                case AimType.Line:
                    FireLine(definition, aimDirection);
                    break;

                case AimType.Fan:
                    FireFan(definition, aimDirection);
                    break;

                case AimType.Throw:
                    FireThrow(definition, aimPoint, aimDirection);
                    break;
            }
        }

        /// <summary>
        /// 直線弾を発射する。
        /// </summary>
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

        /// <summary>
        /// 直線弾の遅延生成処理。
        /// </summary>
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

        /// <summary>
        /// 直線弾を1発生成する。
        /// </summary>
        private void SpawnLineBullet(
            AttackDefinition attackDefinition,
            LineAttackDefinition lineDefinition,
            LineAimLineDefinition aimLineDefinition,
            LineBulletDefinition bulletDefinition,
            Vector3 aimDirection)
        {
            Vector3 baseDirection = GetAimDirection(aimDirection);

            // AimLine 側の角度補正を加えた発射方向。
            Vector3 lineDirection =
                Quaternion.Euler(0f, aimLineDefinition.OffsetAngleFromAimLine, 0f) * baseDirection;

            lineDirection.y = 0f;
            if (lineDirection.sqrMagnitude <= 0.0001f)
            {
                lineDirection = baseDirection;
            }

            lineDirection.Normalize();

            // 発射位置の横ずらし。
            Vector3 rightDirection = GetRight(lineDirection);
            float lateralOffsetMeters =
                aimLineDefinition.OffsetDistanceFromAimLine + bulletDefinition.OffsetFromAimLine;

            // 銃口基準の生成原点を使用する。
            Vector3 spawnOrigin = GetSpawnOrigin();

            // 弾ごとのローカルオフセットを発射方向基準でワールド変換する。
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

        /// <summary>
        /// 扇状弾を発射する。
        /// </summary>
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

        /// <summary>
        /// 扇状弾の遅延生成処理。
        /// </summary>
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

        /// <summary>
        /// 扇状弾を1発生成する。
        /// </summary>
        private void SpawnFanBullet(
            AttackDefinition attackDefinition,
            FanAttackDefinition fanDefinition,
            FanBulletDefinition bulletDefinition,
            Vector3 aimDirection)
        {
            Vector3 baseDirection = GetAimDirection(aimDirection);

            // 中心角からのオフセットを加えた発射方向。
            Vector3 bulletDirection =
                Quaternion.Euler(0f, bulletDefinition.OffsetAngleFromCenter, 0f) * baseDirection;

            bulletDirection.y = 0f;
            if (bulletDirection.sqrMagnitude <= 0.0001f)
            {
                bulletDirection = baseDirection;
            }

            bulletDirection.Normalize();

            // 扇状弾も銃口基準の生成原点を使う。
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

        /// <summary>
        /// 投擲弾の処理。
        /// 現段階では未対応。
        /// </summary>
        private void FireThrow(
            AttackDefinition attackDefinition,
            Vector3 aimPoint,
            Vector3 aimDirection)
        {
            // Throw は別段階で対応。
        }

        /// <summary>
        /// 弾の生成基準位置を返す。
        /// _shotOrigin が設定されていればそれを優先し、
        /// 未設定時は自身の位置を基準にする。
        /// </summary>
        private Vector3 GetSpawnOrigin()
        {
            if (_shotOrigin != null)
            {
                return _shotOrigin.position + Vector3.up * _spawnHeightOffset;
            }

            return transform.position + Vector3.up * _spawnHeightOffset;
        }

        private static IBulletSpawner ResolveBulletSpawnerFromBehaviour(MonoBehaviour behaviour)
        {
            return behaviour as IBulletSpawner;
        }

        private static IBulletSpawner ResolveBulletSpawnerFromComponents(GameObject targetObject)
        {
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

        /// <summary>
        /// エイム方向を XZ 平面上の正規化ベクトルとして返す。
        /// 方向がほぼゼロなら自身の forward を使う。
        /// </summary>
        private Vector3 GetAimDirection(Vector3 aimDirection)
        {
            Vector3 direction = aimDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            return direction.normalized;
        }

        /// <summary>
        /// forward 方向から右方向ベクトルを返す。
        /// </summary>
        private Vector3 GetRight(Vector3 forwardDirection)
        {
            return Quaternion.Euler(0f, 90f, 0f) * forwardDirection.normalized;
        }

        /// <summary>
        /// index に対応する AimLine を返す。
        /// index が範囲外の場合は最後の定義を使う。
        /// </summary>
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

            return definitions[index];
        }

        /// <summary>
        /// 発射方向基準のローカルオフセットをワールド座標へ変換する。
        /// x は右方向、y は上方向、z は前方向として扱う。
        /// </summary>
        private Vector3 TransformLocalSpawnOffset(Vector3 forwardDirection, Vector3 localOffset)
        {
            Vector3 normalizedForward = forwardDirection.normalized;
            Vector3 rightDirection = GetRight(normalizedForward);

            return (rightDirection * localOffset.x)
                + (Vector3.up * localOffset.y)
                + (normalizedForward * localOffset.z);
        }
    }
}
