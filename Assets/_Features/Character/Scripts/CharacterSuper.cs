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

        [SerializeField] private CharacterMove _characterMove;

        [Title("Super")]
        [SerializeField]
        private BulletSpawnSetting[] _superSettings = { new() };

        [SerializeField, Min(0f)]
        private float _attackDisableSeconds = 1f;

        [Tooltip("このスーパーの弾が敵に命中したときに加算する必殺技チャージ率(%)")]
        [SerializeField, Min(0f)]
        private float _superChargePercent = 0f;

        [Title("Super Move")]
        [SerializeField]
        private bool _isSuperMoveEnabled = true;

        [SerializeField, Min(0f)]
        private float _superMoveSpeed = 10f;

        [SerializeField, Min(0f)]
        private float _superMoveDistance = 1f;

        public CharacterSkillPreviewInfo PreviewInfo => CreatePreviewInfo();

        private CharacterSkillPreviewInfo CreatePreviewInfo()
        {
            if (_superSettings == null || _superSettings.Length == 0)
            {
                return default;
            }

            int bulletCount = 0;
            float totalDamage = 0f;
            float maxRange = 0f;
            float maxSpeed = 0f;

            for (int i = 0; i < _superSettings.Length; i++)
            {
                BulletSpawnSetting setting = _superSettings[i];

                if (setting == null || setting.BulletPrefab == null)
                {
                    continue;
                }

                bulletCount++;
                totalDamage += setting.Damage;
                maxRange = Mathf.Max(maxRange, setting.Range);
                maxSpeed = Mathf.Max(maxSpeed, setting.Speed);
            }

            return new CharacterSkillPreviewInfo(
                bulletCount,
                totalDamage,
                maxRange,
                maxSpeed);
        }

        private void Awake()
        {
            if (_status == null)
            {
                TryGetComponent(out _status);
            }

            if (_characterMove == null)
            {
                TryGetComponent(out _characterMove);
            }
        }

        public void Super(Vector3 aimDirectionWorld)
        {
            if (_status == null)
            {
                return;
            }

            if (!_status.TryBeginSuper(_attackDisableSeconds))
            {
                return;
            }

            Vector3 baseDirection = ResolveDirection(aimDirectionWorld);

            _status.Animator.SetTrigger("SuperTrigger");

            BeginSuperMove(baseDirection);
            SpawnBullets(_superSettings, baseDirection);
        }

        private void SpawnBullets(
            BulletSpawnSetting[] settings,
            Vector3 baseDirection)
        {
            if (settings == null || settings.Length == 0)
            {
                return;
            }

            for (int i = 0; i < settings.Length; i++)
            {
                BulletSpawnSetting setting = settings[i];

                if (setting == null)
                {
                    continue;
                }

                if (setting.DelaySeconds <= 0f)
                {
                    SpawnBullet(setting, baseDirection);
                    continue;
                }

                StartCoroutine(SpawnBulletAfterDelay(setting, baseDirection));
            }
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
            if (setting == null || setting.BulletPrefab == null)
            {
                return;
            }

            Vector3 direction = ApplyAngleOffset(
                baseDirection,
                setting.AngleOffsetDegrees);

            Vector3 spawnPosition = GetSpawnPosition(
                direction,
                setting.PositionOffsetLocal);

            Quaternion spawnRotation =
                Quaternion.LookRotation(direction, Vector3.up);

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
                    gameObject,
                    _superChargePercent,
                    setting.IsPiercing,
                    setting.ShouldHealOwnerOnHit,
                    setting.OwnerHealPercent);
            }
        }


        private void BeginSuperMove(Vector3 direction)
        {
            if (!_isSuperMoveEnabled || _characterMove == null)
            {
                return;
            }

            _characterMove.BeginSkillMove(
                direction,
                _superMoveSpeed,
                _superMoveDistance);
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
            Quaternion rotation =
                Quaternion.AngleAxis(angleOffsetDegrees, Vector3.up);

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

            Vector3 right =
                Vector3.Cross(Vector3.up, forward).normalized;

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

            [SerializeField] private bool _isPiercing;

            [Tooltip("この弾が敵に命中したとき、自身を回復するか")]
            [SerializeField] private bool _shouldHealOwnerOnHit;

            [Tooltip("自身のMaxHpに対する回復割合(%)。10なら最大HPの10%回復")]
            [Min(0f)]
            [SerializeField] private float _ownerHealPercent;

            [Min(0f)]
            [SerializeField] private float _delaySeconds;

            public GameObject BulletPrefab => _bulletPrefab;
            public float Damage => _damage;
            public float Range => _range;
            public float Speed => _speed;
            public float AngleOffsetDegrees => _angleOffsetDegrees;
            public Vector3 PositionOffsetLocal => _positionOffsetLocal;
            public bool IsPiercing => _isPiercing;
            public bool ShouldHealOwnerOnHit => _shouldHealOwnerOnHit;
            public float OwnerHealPercent => _ownerHealPercent;
            public float DelaySeconds => _delaySeconds;
        }
    }
}
