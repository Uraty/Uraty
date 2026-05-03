using System.Collections.Generic;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterBullet : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("Collision")]
        [SerializeField] private LayerMask _characterLayers;
        [SerializeField] private LayerMask _blockingLayers;

        [Header("Pierce")]
        [SerializeField] private bool _isPiercing;

        private readonly HashSet<int> _hitCharacterInstanceIds = new();

        private Vector3 _direction;
        private float _damage;
        private float _range;
        private float _speed;

        private Vector3 _startPosition;
        private CharacterStatus _ownerStatus;
        private bool _isInitialized;

        public float Damage => _damage;

        public void Initialize(
            Vector3 direction,
            float damage,
            float range,
            float speed,
            GameObject ownerObject)
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

            _startPosition = transform.position;
            _ownerStatus = ResolveOwnerStatus(ownerObject);

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

            int otherLayerMask = 1 << other.gameObject.layer;

            if ((_blockingLayers.value & otherLayerMask) != 0)
            {
                Destroy(gameObject);
                return;
            }

            if ((_characterLayers.value & otherLayerMask) != 0)
            {
                HitCharacter(other);
            }
        }

        private void HitCharacter(Collider other)
        {
            CharacterStatus targetStatus = other.GetComponentInParent<CharacterStatus>();
            if (targetStatus == null)
            {
                return;
            }

            if (ShouldIgnoreTarget(targetStatus))
            {
                return;
            }

            int targetInstanceId = targetStatus.GetInstanceID();

            if (_hitCharacterInstanceIds.Contains(targetInstanceId))
            {
                return;
            }

            _hitCharacterInstanceIds.Add(targetInstanceId);

            targetStatus.ApplyDamage(_damage);

            if (!_isPiercing)
            {
                Destroy(gameObject);
            }
        }

        private bool ShouldIgnoreTarget(CharacterStatus targetStatus)
        {
            if (targetStatus.IsDead)
            {
                return true;
            }

            if (_ownerStatus == null)
            {
                return false;
            }

            if (targetStatus == _ownerStatus)
            {
                return true;
            }

            if (targetStatus.IsSameTeam(_ownerStatus.TeamId))
            {
                return true;
            }

            return false;
        }

        private static CharacterStatus ResolveOwnerStatus(GameObject ownerObject)
        {
            if (ownerObject == null)
            {
                return null;
            }

            if (ownerObject.TryGetComponent(out CharacterStatus characterStatus))
            {
                return characterStatus;
            }

            return ownerObject.GetComponentInParent<CharacterStatus>();
        }

        private float GetMovedDistance()
        {
            Vector3 delta = transform.position - _startPosition;
            delta.y = 0f;

            return delta.magnitude;
        }
    }
}
