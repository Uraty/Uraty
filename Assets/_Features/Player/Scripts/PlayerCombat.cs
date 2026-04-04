using System.Collections;

using UnityEngine;

namespace Uraty.Feature.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        private const float MinValue = 0.0001f;

        [SerializeField] private PlayerStatus _playerStatus;
        [SerializeField] private PlayerAim _playerAim;
        [SerializeField] private Transform _shotOrigin;

        [Header("Spawn")]
        [SerializeField] private float _spawnHeightOffset = 0.5f;

        private float _nextAttackTime;
        private float _nextSpecialTime;

        private void Reset()
        {
            _playerStatus = GetComponent<PlayerStatus>();
            _playerAim = GetComponent<PlayerAim>();
            _shotOrigin = transform;
        }

        private void LateUpdate()
        {
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

        private void TryHandleAttack()
        {
            if (!_playerAim.TryConsumeAttack(out Vector3 aimPoint, out Vector3 aimDirection))
            {
                return;
            }

            AttackDefinition attack = _playerStatus.RoleDefinition.Attack;
            if (attack == null)
            {
                return;
            }

            if (Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime = Time.time + Mathf.Max(0f, attack.AttackIntervalSeconds);
            ExecuteDefinition(attack, aimPoint, aimDirection);
        }

        private void TryHandleSpecial()
        {
            if (!_playerAim.TryConsumeSpecial(out Vector3 aimPoint, out Vector3 aimDirection))
            {
                return;
            }

            AttackDefinition special = _playerStatus.RoleDefinition.Special;
            if (special == null)
            {
                return;
            }

            if (Time.time < _nextSpecialTime)
            {
                return;
            }

            _nextSpecialTime = Time.time + Mathf.Max(0f, special.AttackIntervalSeconds);
            ExecuteDefinition(special, aimPoint, aimDirection);
        }

        private void ExecuteDefinition(AttackDefinition definition, Vector3 aimPoint, Vector3 aimDirection)
        {
            switch (definition.Type)
            {
                case AimType.Line:
                    FireLine(definition.Line, aimDirection);
                    break;

                case AimType.Fan:
                    FireFan(definition.Fan, aimDirection);
                    break;

                case AimType.Throw:
                    FireThrow(definition.Throw, aimPoint, aimDirection);
                    break;
            }
        }

        private void FireLine(LineAttackDefinition definition, Vector3 aimDirection)
        {
            if (definition == null)
            {
                return;
            }

            LineBulletDefinition[] bullets = definition.Bullets;
            if (bullets == null || bullets.Length == 0)
            {
                return;
            }

            for (int i = 0; i < bullets.Length; i++)
            {
                LineBulletDefinition bullet = bullets[i];
                if (bullet == null || bullet.BulletPrefab == null)
                {
                    continue;
                }

                LineAimLineDefinition aimLine = GetAssignedAimLine(definition.AimLines, i);
                if (aimLine == null)
                {
                    continue;
                }

                StartCoroutine(SpawnLineProjectileDelayed(definition, aimLine, bullet, aimDirection));
            }
        }

        private IEnumerator SpawnLineProjectileDelayed(
            LineAttackDefinition definition,
            LineAimLineDefinition aimLine,
            LineBulletDefinition bullet,
            Vector3 aimDirection)
        {
            float delay = Mathf.Max(0f, bullet.SpawnDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            SpawnLineProjectile(definition, aimLine, bullet, aimDirection);
        }

        private void SpawnLineProjectile(
            LineAttackDefinition definition,
            LineAimLineDefinition aimLine,
            LineBulletDefinition bullet,
            Vector3 aimDirection)
        {
            Vector3 lineDirection = Quaternion.Euler(0f, aimLine.OffsetAngleFromAimLine, 0f) * GetAimDirection(aimDirection);
            lineDirection.y = 0f;

            if (lineDirection.sqrMagnitude <= MinValue)
            {
                lineDirection = GetAimDirection(aimDirection);
            }

            lineDirection.Normalize();

            Vector3 right = GetRight(lineDirection);
            float totalOffset = aimLine.OffsetDistanceFromAimLine + bullet.OffsetFromAimLine;
            Vector3 lateralOffset = right * totalOffset;

            Vector3 start = GetSpawnOrigin() + lateralOffset;
            Vector3 end = GetGroundOrigin() + lateralOffset + (lineDirection * Mathf.Max(0f, aimLine.EffectiveRange));
            end.y = start.y;

            GameObject projectile = Instantiate(
                bullet.BulletPrefab,
                start,
                GetLookRotation(lineDirection));

            ApplyBoxScale(projectile.transform, bullet.BulletWidth, bullet.BulletHeight);

            float speed = Mathf.Max(MinValue, definition.SpeedPerSecond);
            StartCoroutine(MoveLinear(projectile.transform, start, end, speed));
        }

        private void FireFan(FanAttackDefinition definition, Vector3 aimDirection)
        {
            if (definition == null)
            {
                return;
            }

            FanBulletDefinition[] bullets = definition.Bullets;
            if (bullets == null || bullets.Length == 0)
            {
                return;
            }

            for (int i = 0; i < bullets.Length; i++)
            {
                FanBulletDefinition bullet = bullets[i];
                if (bullet == null || bullet.BulletPrefab == null)
                {
                    continue;
                }

                FanAimLineDefinition aimLine = GetAssignedAimLine(definition.AimLines, i);
                if (aimLine == null)
                {
                    continue;
                }

                StartCoroutine(SpawnFanProjectileDelayed(definition, aimLine, bullet, aimDirection));
            }
        }

        private IEnumerator SpawnFanProjectileDelayed(
            FanAttackDefinition definition,
            FanAimLineDefinition aimLine,
            FanBulletDefinition bullet,
            Vector3 aimDirection)
        {
            float delay = Mathf.Max(0f, bullet.SpawnDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            SpawnFanProjectile(definition, aimLine, bullet, aimDirection);
        }

        private void SpawnFanProjectile(
            FanAttackDefinition definition,
            FanAimLineDefinition aimLine,
            FanBulletDefinition bullet,
            Vector3 aimDirection)
        {
            float totalAngleOffset = aimLine.OffsetAngleFromAimLine + bullet.OffsetAngleFromAimLine;

            Vector3 direction = Quaternion.Euler(0f, totalAngleOffset, 0f) * GetAimDirection(aimDirection);
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinValue)
            {
                direction = GetAimDirection(aimDirection);
            }

            direction.Normalize();

            Vector3 start = GetSpawnOrigin();
            Vector3 end = GetGroundOrigin() + (direction * Mathf.Max(0f, aimLine.EffectiveRange));
            end.y = start.y;

            GameObject projectile = Instantiate(
                bullet.BulletPrefab,
                start,
                GetLookRotation(direction));

            float width = GetFanVisualWidth(bullet.Height, bullet.Angle);
            ApplyBoxScale(projectile.transform, width, bullet.Height);

            float speed = Mathf.Max(MinValue, definition.SpeedPerSecond);
            StartCoroutine(MoveLinear(projectile.transform, start, end, speed));
        }

        private void FireThrow(ThrowAttackDefinition definition, Vector3 aimPoint, Vector3 aimDirection)
        {
            if (definition == null)
            {
                return;
            }

            ThrowBulletDefinition[] bullets = definition.Bullets;
            if (bullets == null || bullets.Length == 0)
            {
                return;
            }

            float currentAimDistance = GetPlanarDistance(GetGroundOrigin(), aimPoint);

            for (int i = 0; i < bullets.Length; i++)
            {
                ThrowBulletDefinition bullet = bullets[i];
                if (bullet == null || bullet.BulletPrefab == null)
                {
                    continue;
                }

                ThrowAimLineDefinition aimLine = GetAssignedAimLine(definition.AimLines, i);
                if (aimLine == null)
                {
                    continue;
                }

                StartCoroutine(SpawnThrowProjectileDelayed(definition, aimLine, bullet, currentAimDistance, aimDirection));
            }
        }

        private IEnumerator SpawnThrowProjectileDelayed(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            ThrowBulletDefinition bullet,
            float currentAimDistance,
            Vector3 aimDirection)
        {
            float delay = Mathf.Max(0f, bullet.SpawnDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            SpawnThrowProjectile(definition, aimLine, bullet, currentAimDistance, aimDirection);
        }

        private void SpawnThrowProjectile(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            ThrowBulletDefinition bullet,
            float currentAimDistance,
            Vector3 aimDirection)
        {
            Vector3 groundOrigin = GetGroundOrigin();
            Vector3 start = GetSpawnOrigin();
            Vector3 baseAimDirection = GetAimDirection(aimDirection);

            Vector3 aimLineDirection = Quaternion.Euler(0f, aimLine.OffsetAngleFromAimLine, 0f) * baseAimDirection;
            aimLineDirection.y = 0f;

            if (aimLineDirection.sqrMagnitude <= MinValue)
            {
                aimLineDirection = baseAimDirection;
            }

            aimLineDirection.Normalize();

            float baseDistance = GetThrowBaseDistance(definition, aimLine, currentAimDistance);
            float aimLineDistance = Mathf.Max(0f, baseDistance + aimLine.OffsetDistanceFromAimLine);

            Vector3 circleCenterPoint = groundOrigin + (aimLineDirection * aimLineDistance);
            circleCenterPoint.y = groundOrigin.y;

            Vector3 offsetDirection = Quaternion.Euler(0f, bullet.OffsetAngleFromAimLine, 0f) * aimLineDirection;
            offsetDirection.y = 0f;

            if (offsetDirection.sqrMagnitude <= MinValue)
            {
                offsetDirection = aimLineDirection;
            }

            offsetDirection.Normalize();

            Vector3 landingPoint = circleCenterPoint + (offsetDirection * Mathf.Max(0f, bullet.OffsetDistanceFromAimLine));
            landingPoint.y = groundOrigin.y;

            GameObject projectile = Instantiate(
                bullet.BulletPrefab,
                start,
                GetLookRotation(circleCenterPoint - start));

            ApplyCircleScale(projectile.transform, bullet.Radius);

            float duration = GetThrowDuration(definition, aimLine, groundOrigin, landingPoint);
            float arcHeight = GetThrowArcHeight(definition, aimLine, groundOrigin, landingPoint);

            StartCoroutine(MoveThrowParabola(
                projectile.transform,
                start,
                circleCenterPoint,
                landingPoint,
                duration,
                arcHeight));
        }

        private float GetThrowBaseDistance(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            float currentAimDistance)
        {
            if (!definition.IsVariableRange)
            {
                return Mathf.Max(0f, aimLine.EffectiveRange);
            }

            return Mathf.Min(Mathf.Max(0f, currentAimDistance), Mathf.Max(0f, aimLine.EffectiveRange));
        }

        private IEnumerator MoveLinear(Transform projectile, Vector3 start, Vector3 end, float speed)
        {
            if (projectile == null)
            {
                yield break;
            }

            Vector3 direction = end - start;
            float distance = direction.magnitude;

            if (distance <= MinValue)
            {
                projectile.position = end;
                OnProjectileArrived(projectile.gameObject);
                yield break;
            }

            Quaternion rotation = GetLookRotation(direction.normalized);
            projectile.rotation = rotation;

            float duration = distance / Mathf.Max(MinValue, speed);
            float elapsedSeconds = 0f;

            while (elapsedSeconds < duration)
            {
                if (projectile == null)
                {
                    yield break;
                }

                elapsedSeconds += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedSeconds / duration);
                projectile.position = Vector3.Lerp(start, end, t);
                projectile.rotation = rotation;

                yield return null;
            }

            if (projectile == null)
            {
                yield break;
            }

            projectile.position = end;
            OnProjectileArrived(projectile.gameObject);
        }

        private IEnumerator MoveThrowParabola(
            Transform projectile,
            Vector3 start,
            Vector3 circleCenterPoint,
            Vector3 landingPoint,
            float duration,
            float arcHeight)
        {
            if (projectile == null)
            {
                yield break;
            }

            float safeDuration = Mathf.Max(MinValue, duration);
            float elapsedSeconds = 0f;

            Vector3 offsetFromCircleCenter = landingPoint - circleCenterPoint;

            while (elapsedSeconds < safeDuration)
            {
                if (projectile == null)
                {
                    yield break;
                }

                elapsedSeconds += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedSeconds / safeDuration);

                Vector3 position = EvaluateThrowPosition(
                    start,
                    circleCenterPoint,
                    offsetFromCircleCenter,
                    arcHeight,
                    t);

                projectile.position = position;

                float nextT = Mathf.Clamp01((elapsedSeconds + 0.01f) / safeDuration);
                Vector3 nextPosition = EvaluateThrowPosition(
                    start,
                    circleCenterPoint,
                    offsetFromCircleCenter,
                    arcHeight,
                    nextT);

                Vector3 forward = nextPosition - position;
                if (forward.sqrMagnitude > MinValue)
                {
                    projectile.rotation = GetLookRotation(forward.normalized);
                }

                yield return null;
            }

            if (projectile == null)
            {
                yield break;
            }

            projectile.position = landingPoint;
            OnProjectileArrived(projectile.gameObject);
        }

        private Vector3 EvaluateThrowPosition(
            Vector3 start,
            Vector3 circleCenterPoint,
            Vector3 offsetFromCircleCenter,
            float arcHeight,
            float t)
        {
            Vector3 point = EvaluateParabola(start, circleCenterPoint, arcHeight, t);
            point += offsetFromCircleCenter * t;
            return point;
        }

        private Vector3 EvaluateParabola(Vector3 start, Vector3 end, float height, float t)
        {
            Vector3 point = Vector3.Lerp(start, end, t);
            point.y += 4f * height * t * (1f - t);
            return point;
        }

        private float GetThrowDuration(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            Vector3 origin,
            Vector3 landingPoint)
        {
            if (!definition.IsVariableLandingTime)
            {
                return Mathf.Max(MinValue, definition.FixedLandingTimeSeconds);
            }

            float maxRange = Mathf.Max(MinValue, aimLine.EffectiveRange);
            float distance = GetPlanarDistance(origin, landingPoint);
            float ratio = Mathf.Clamp01(distance / maxRange);

            float minSeconds = Mathf.Min(definition.MinLandingTimeSeconds, definition.MaxLandingTimeSeconds);
            float maxSeconds = Mathf.Max(definition.MinLandingTimeSeconds, definition.MaxLandingTimeSeconds);

            return Mathf.Max(MinValue, Mathf.Lerp(minSeconds, maxSeconds, ratio));
        }

        private float GetThrowArcHeight(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            Vector3 origin,
            Vector3 landingPoint)
        {
            if (!definition.IsVariableLandingTime)
            {
                return Mathf.Max(0f, aimLine.FixedParabolaHeight);
            }

            float maxRange = Mathf.Max(MinValue, aimLine.EffectiveRange);
            float distance = GetPlanarDistance(origin, landingPoint);
            float ratio = Mathf.Clamp01(distance / maxRange);

            float minHeight = Mathf.Min(aimLine.MinParabolaHeight, aimLine.MaxParabolaHeight);
            float maxHeight = Mathf.Max(aimLine.MinParabolaHeight, aimLine.MaxParabolaHeight);

            return Mathf.Lerp(minHeight, maxHeight, ratio);
        }

        private float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            delta.y = 0f;
            return delta.magnitude;
        }

        private float GetFanVisualWidth(float height, float angle)
        {
            float safeHeight = Mathf.Max(0.01f, height);
            float halfAngleRad = Mathf.Max(0f, angle) * 0.5f * Mathf.Deg2Rad;
            float width = 2f * safeHeight * Mathf.Tan(halfAngleRad);
            return Mathf.Max(0.01f, width);
        }

        private void ApplyBoxScale(Transform target, float width, float height)
        {
            if (target == null)
            {
                return;
            }

            Vector3 scale = target.localScale;
            scale.x = Mathf.Max(0.01f, width);
            scale.z = Mathf.Max(0.01f, height);
            target.localScale = scale;
        }

        private void ApplyCircleScale(Transform target, float radius)
        {
            if (target == null)
            {
                return;
            }

            float diameter = Mathf.Max(0.01f, radius * 2f);

            Vector3 scale = target.localScale;
            scale.x = diameter;
            scale.z = diameter;
            target.localScale = scale;
        }

        private void OnProjectileArrived(GameObject projectile)
        {
            if (projectile == null)
            {
                return;
            }

            Destroy(projectile);
        }

        private Vector3 GetGroundOrigin()
        {
            return transform.position;
        }

        private Vector3 GetSpawnOrigin()
        {
            if (_shotOrigin != null)
            {
                return _shotOrigin.position;
            }

            return transform.position + (Vector3.up * _spawnHeightOffset);
        }

        private Vector3 GetAimDirection(Vector3 aimDirection)
        {
            Vector3 direction = aimDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinValue)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            return direction.sqrMagnitude > MinValue
                ? direction.normalized
                : Vector3.forward;
        }

        private Vector3 GetRight(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude <= MinValue)
            {
                return transform.right;
            }

            return right.normalized;
        }

        private Quaternion GetLookRotation(Vector3 forward)
        {
            Vector3 safeForward = forward;
            safeForward.y = 0f;

            if (safeForward.sqrMagnitude <= MinValue)
            {
                safeForward = Vector3.forward;
            }

            return Quaternion.LookRotation(safeForward.normalized, Vector3.up);
        }

        private T GetAssignedAimLine<T>(T[] aimLines, int bulletIndex) where T : class
        {
            if (aimLines == null || aimLines.Length == 0)
            {
                return null;
            }

            int index = bulletIndex % aimLines.Length;
            if (aimLines[index] != null)
            {
                return aimLines[index];
            }

            for (int i = 0; i < aimLines.Length; i++)
            {
                if (aimLines[i] != null)
                {
                    return aimLines[i];
                }
            }

            return null;
        }
    }
}
