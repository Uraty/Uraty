using System;

using TriInspector;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterAttack : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        private const float SpawnForwardOffset = 0.5f;

        [Title("Attack")]
        [SerializeField, InlineProperty, HideLabel]
        private BulletSpawnSetting _attackSetting = new();

        public void Attack(Vector3 aimDirectionWorld)
        {
            SpawnBullet(_attackSetting, aimDirectionWorld);
        }

        private void SpawnBullet(
            BulletSpawnSetting setting,
            Vector3 aimDirectionWorld)
        {
            if (setting == null || setting.BulletPrefab == null)
            {
                return;
            }

            Vector3 direction = ResolveDirection(aimDirectionWorld);
            Vector3 spawnPosition = GetSpawnPosition(direction);
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
                    setting.Speed);
            }
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

        private Vector3 GetSpawnPosition(Vector3 direction)
        {
            return transform.position + direction * Mathf.Max(0f, SpawnForwardOffset);
        }

        [Serializable]
        private sealed class BulletSpawnSetting
        {
            [SerializeField] private GameObject _bulletPrefab;

            [Min(0f)]
            [SerializeField] private float _damage = 10f;

            [Min(0f)]
            [SerializeField] private float _range = 8f;

            [Min(0f)]
            [SerializeField] private float _speed = 16f;

            public GameObject BulletPrefab => _bulletPrefab;
            public float Damage => _damage;
            public float Range => _range;
            public float Speed => _speed;
        }
    }
}
