using System.Collections.Generic;

using UnityEngine;

using Uraty.Shared.Hit;
using Uraty.Shared.Team;

namespace Uraty.Features.Character
{
    public sealed class CharacterBullet : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        private readonly HashSet<int> _hitCharacterInstanceIds = new();

        private Vector3 _direction;
        private float _damage;
        private float _range;
        private float _speed;
        private float _superChargePercent;
        private bool _isPiercing;
        private bool _shouldHealOwnerOnHit;
        private float _ownerHealPercent;

        private Vector3 _startPosition;
        private GameObject _owner;
        private TeamId _teamId;
        private bool _isInitialized;

        public float Damage => _damage;

        public void Initialize(
            Vector3 direction,
            float damage,
            float range,
            float speed,
            TeamId teamId,
            GameObject owner,
            float superChargePercent,
            bool isPiercing,
            bool shouldHealOwnerOnHit,
            float ownerHealPercent)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = Vector3.forward;
            }

            _direction = direction.normalized;
            _damage = Mathf.Max(0f, damage);
            _range = Mathf.Max(0f, range);
            _speed = Mathf.Max(0f, speed);
            _superChargePercent = Mathf.Max(0f, superChargePercent);
            _isPiercing = isPiercing;
            _shouldHealOwnerOnHit = shouldHealOwnerOnHit;
            _ownerHealPercent = Mathf.Max(0f, ownerHealPercent);

            _startPosition = transform.position;
            _owner = owner;
            _teamId = teamId;

            _hitCharacterInstanceIds.Clear();
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            transform.position += _direction * _speed * Time.deltaTime;

            if (GetMovedDistance() >= _range)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isInitialized)
            {
                return;
            }

            if (!other.TryGetComponent(out IBulletHittable hittable))
            {
                return;
            }

            CharacterStatus targetStatus = null;
            other.TryGetComponent(out targetStatus);

            if (targetStatus != null)
            {
                int targetInstanceId = targetStatus.GetInstanceID();

                if (_hitCharacterInstanceIds.Contains(targetInstanceId))
                {
                    return;
                }
            }

            bool shouldApplyHitEffects =
                ShouldApplyHitEffects(targetStatus);

            bool shouldAddSuperCharge =
                shouldApplyHitEffects &&
                _superChargePercent > 0f;

            bool shouldHealOwner =
                shouldApplyHitEffects &&
                _shouldHealOwnerOnHit &&
                _ownerHealPercent > 0f;

            bool shouldDestroy =
                hittable.ReceiveBulletHit(
                    _owner,
                    _teamId,
                    _damage,
                    _isPiercing);

            if (shouldApplyHitEffects && targetStatus != null)
            {
                _hitCharacterInstanceIds.Add(
                    targetStatus.GetInstanceID());
            }

            if (shouldAddSuperCharge)
            {
                AddSuperChargeToOwner();
            }

            if (shouldHealOwner)
            {
                HealOwnerByPercent();
            }

            if (shouldDestroy)
            {
                Destroy(gameObject);
            }
        }

        private bool ShouldApplyHitEffects(CharacterStatus targetStatus)
        {
            if (targetStatus == null)
            {
                return false;
            }

            if (targetStatus.IsDead)
            {
                return false;
            }

            if (targetStatus.TeamId == _teamId)
            {
                return false;
            }

            if (_damage <= 0f)
            {
                return false;
            }

            return true;
        }

        private void AddSuperChargeToOwner()
        {
            if (_owner == null)
            {
                return;
            }

            if (!_owner.TryGetComponent(out CharacterStatus ownerStatus))
            {
                return;
            }

            ownerStatus.AddSuperCharge(_superChargePercent);
        }

        private void HealOwnerByPercent()
        {
            if (_owner == null)
            {
                return;
            }

            if (!_owner.TryGetComponent(out CharacterStatus ownerStatus))
            {
                return;
            }

            float healAmount =
                ownerStatus.MaxHp *
                (_ownerHealPercent / 100f);

            ownerStatus.Heal(healAmount);
        }

        private float GetMovedDistance()
        {
            Vector3 delta = transform.position - _startPosition;
            delta.y = 0f;

            return delta.magnitude;
        }
    }
}
