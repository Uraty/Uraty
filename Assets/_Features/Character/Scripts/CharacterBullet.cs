using System.Collections.Generic;

using UnityEngine;

using Uraty.Shared.Hit;
using Uraty.Shared.Team;

namespace Uraty.Features.Character
{
    public sealed class CharacterBullet : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("Pierce")]
        [SerializeField] private bool _isPiercing;

        private readonly HashSet<int> _hitCharacterInstanceIds = new();

        private Vector3 _direction;
        private float _damage;
        private float _range;
        private float _speed;
        private float _superChargePercent;

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
            float superChargePercent)
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

            bool shouldAddSuperCharge =
                ShouldAddSuperCharge(targetStatus);

            bool shouldDestroy =
                hittable.ReceiveBulletHit(
                    _owner,
                    _teamId,
                    _damage,
                    _isPiercing);

            if (shouldAddSuperCharge)
            {
                if (targetStatus != null)
                {
                    _hitCharacterInstanceIds.Add(
                        targetStatus.GetInstanceID());
                }

                AddSuperChargeToOwner();
            }

            if (shouldDestroy)
            {
                Destroy(gameObject);
            }
        }

        private bool ShouldAddSuperCharge(CharacterStatus targetStatus)
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

            if (_superChargePercent <= 0f)
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

        private float GetMovedDistance()
        {
            Vector3 delta = transform.position - _startPosition;
            delta.y = 0f;

            return delta.magnitude;
        }
    }
}
