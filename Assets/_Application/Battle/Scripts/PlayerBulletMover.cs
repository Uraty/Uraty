using UnityEngine;

using Uraty.Shared.Battle;

namespace Uraty.Application.Battle
{
    [RequireComponent(typeof(PlayerBullet))]
    public sealed class PlayerBulletMover : MonoBehaviour
    {
        private const float MinMoveDistanceMeters = 0.0001f;

        [SerializeField] private float _hitRadiusMeters = 0.1f;

        private BulletRuntimeData _runtimeData;
        private PlayerBullet _playerBullet;
        private float _traveledDistanceMeters;
        private bool _isInitialized;

        public void Initialize(BulletRuntimeData runtimeData)
        {
            _runtimeData = runtimeData;
            _playerBullet = GetComponent<PlayerBullet>();
            _traveledDistanceMeters = 0f;
            _isInitialized = true;

            transform.position = runtimeData.StartPosition;

            if (runtimeData.Direction.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(runtimeData.Direction);
            }
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            TickMove(Time.deltaTime);
        }

        private void TickMove(float deltaTime)
        {
            float moveDistanceMeters = _runtimeData.SpeedMetersPerSecond * Mathf.Max(0f, deltaTime);
            if (moveDistanceMeters <= MinMoveDistanceMeters)
            {
                return;
            }

            Vector3 moveDirection = _runtimeData.Direction.normalized;
            Vector3 moveVector = moveDirection * moveDistanceMeters;

            if (TryDetectHit(moveDirection, moveDistanceMeters, out RaycastHit hit))
            {
                HandleHit(hit);
                return;
            }

            transform.position += moveVector;
            _traveledDistanceMeters += moveDistanceMeters;

            if (_traveledDistanceMeters >= _runtimeData.MaxTravelDistanceMeters)
            {
                Destroy(gameObject);
            }
        }

        private bool TryDetectHit(Vector3 moveDirection, float moveDistanceMeters, out RaycastHit hit)
        {
            return Physics.SphereCast(
                origin: transform.position,
                radius: _hitRadiusMeters,
                direction: moveDirection,
                hitInfo: out hit,
                maxDistance: moveDistanceMeters);
        }

        private void HandleHit(RaycastHit hit)
        {
            IBulletHittable hittable = hit.collider.GetComponentInParent<IBulletHittable>();
            if (hittable == null)
            {
                Destroy(gameObject);
                return;
            }

            BulletHitContext context = _playerBullet.CreateHitContext(hit.point);
            BulletHitResponse response = hittable.ReceiveBulletHit(context);

            _playerBullet.ApplyHitResponse(response, hit.collider.gameObject);
        }
    }
}
