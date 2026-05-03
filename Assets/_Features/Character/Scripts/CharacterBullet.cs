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
            GameObject owner)
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

            if (other.TryGetComponent(out IBulletHittable hittable))
            {
                if (hittable.ReceiveBulletHit(_owner, _teamId, _damage, _isPiercing))
                {
                    Destroy(gameObject);
                }
            }
        }

        private float GetMovedDistance()
        {
            Vector3 delta = transform.position - _startPosition;
            delta.y = 0f;

            return delta.magnitude;
        }
    }
}
