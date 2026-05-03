using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterBullet : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        private Vector3 _direction;
        private float _damage;
        private float _range;
        private float _speed;

        private Vector3 _startPosition;
        private bool _isInitialized;

        public float Damage => _damage;

        public void Initialize(
            Vector3 direction,
            float damage,
            float range,
            float speed)
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

        private float GetMovedDistance()
        {
            Vector3 delta = transform.position - _startPosition;
            delta.y = 0f;

            return delta.magnitude;
        }
    }
}
