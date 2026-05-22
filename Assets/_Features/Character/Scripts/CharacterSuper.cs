using System;
using System.Collections;

using TriInspector;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterSuper : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;
        private const float SpawnForwardOffset = 0.5f;

        [SerializeField] private CharacterStatus _status;

        [Title("Super")]
        [SerializeField]
        private BulletSpawnSetting[] _superSettings = { new() };

        private void Awake()
        {
            if (_status == null)
            {
                TryGetComponent(out _status);
            }
        }

        public void Super(Vector3 aimDirectionWorld)
        {
            if (_status == null || _status.IsDead)
            {
                return;
            }

            bool didSuper =
                TrySpawnBullets(
                    _superSettings,
                    aimDirectionWorld);

            if (didSuper)
            {
                _status.NotifyAttackPerformed();
            }
        }

        private bool TrySpawnBullets(
            BulletSpawnSetting[] settings,
            Vector3 aimDirectionWorld)
        {
            if (settings == null || settings.Length == 0)
            {
                return false;
            }

            Vector3 baseDirection = ResolveDirection(aimDirectionWorld);

            bool didSuper = false;

            for (int i = 0; i < settings.Length; i++)
            {
                BulletSpawnSetting setting = settings[i];

                if (!CanSpawn(setting))
                {
                    continue;
                }

                didSuper = true;

                if (setting.DelaySeconds <= 0f)
                {
                    SpawnBullet(setting, baseDirection);
                    continue;
                }

                StartCoroutine(SpawnBulletAfterDelay(setting, baseDirection));
            }

            return didSuper;
        }

        private IEnumerator SpawnBulletAfterDelay(
            BulletSpawnSetting setting,
            Vector3 baseDirection)
        {
            yield return new WaitForSeconds(setting.DelaySeconds);

            SpawnBullet(setting, baseDirection);
        }

        private void SpawnBullet(
            BulletSpawnSetting setting,
            Vector3 baseDirection)
        {
            if (!CanSpawn(setting))
            {
                return;
            }

            Vector3 direction = ApplyAngleOffset(
                baseDirection,
                setting.AngleOffsetDegrees);

            Vector3 spawnPosition = GetSpawnPosition(
                direction,
                setting.PositionOffsetLocal);

            Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);

            GameObject bulletObject = Instantiate(
                setting.BulletPrefab,
                spawnPosition,
                spawnRotation);

            if (bulletObject.TryGetComponent(out CharacterBullet bullet))
            {
                bullet.Initialize(
                    direction,
                    setting.Damage,
                    setting.Range,
                    setting.Speed,
                    _status.TeamId,
                    gameObject);
            }
        }

        private static bool CanSpawn(BulletSpawnSetting setting)
        {
            return setting != null && setting.BulletPrefab != null;
        }

        private Vector3 ResolveDirection(Vector3 aimDirectionWorld)
        {
            aimDirectionWorld.y = 0f;

            if (aimDirectionWorld.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                return aimDirectionWorld.normalized;
            }

            Vector3 fallbackDirection = transform.forward;
            fallbackDirection.y = 0f;

            if (fallbackDirection.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                return fallbackDirection.normalized;
            }

            return Vector3.forward;
        }

        private static Vector3 ApplyAngleOffset(
            Vector3 direction,
            float angleOffsetDegrees)
        {
            Quaternion rotation = Quaternion.AngleAxis(angleOffsetDegrees, Vector3.up);
            Vector3 rotatedDirection = rotation * direction;
            rotatedDirection.y = 0f;

            if (rotatedDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return Vector3.forward;
            }

            return rotatedDirection.normalized;
        }

        private Vector3 GetSpawnPosition(
            Vector3 direction,
            Vector3 positionOffsetLocal)
        {
            Vector3 forward = direction;
            forward.y = 0f;

            if (forward.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 basePosition =
                transform.position +
                forward * Mathf.Max(0f, SpawnForwardOffset);

            return
                basePosition +
                right * positionOffsetLocal.x +
                Vector3.up * positionOffsetLocal.y +
                forward * positionOffsetLocal.z;
        }

        [Serializable]
        private sealed class BulletSpawnSetting
        {
            [SerializeField] private GameObject _bulletPrefab;

            [Min(0f)]
            [SerializeField] private float _damage = 30f;

            [Min(0f)]
            [SerializeField] private float _range = 10f;

            [Min(0f)]
            [SerializeField] private float _speed = 20f;

            [SerializeField] private float _angleOffsetDegrees;

            [SerializeField] private Vector3 _positionOffsetLocal;

            [Min(0f)]
            [SerializeField] private float _delaySeconds;

            public GameObject BulletPrefab => _bulletPrefab;
            public float Damage => _damage;
            public float Range => _range;
            public float Speed => _speed;
            public float AngleOffsetDegrees => _angleOffsetDegrees;
            public Vector3 PositionOffsetLocal => _positionOffsetLocal;
            public float DelaySeconds => _delaySeconds;
        }
    }
}
