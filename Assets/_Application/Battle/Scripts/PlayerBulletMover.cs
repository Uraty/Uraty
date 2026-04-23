using UnityEngine;

using Uraty.Shared.Battle;

namespace Uraty.Application.Battle
{
    [RequireComponent(typeof(PlayerBullet))]
    public sealed class PlayerBulletMover : MonoBehaviour
    {
        private const float MinMoveDistanceMeters = 0.0001f;
        private const float PassThroughEpsilonMeters = 0.01f;

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
                bool canContinue = HandleHit(hit);
                if (!canContinue)
                {
                    return;
                }

                // 通行可能なら少し先へ押し出して、同じコライダーへの連続ヒットを避ける
                float continueDistanceMeters = Mathf.Max(
                    0f,
                    moveDistanceMeters - hit.distance + PassThroughEpsilonMeters);

                Vector3 continueVector = moveDirection * continueDistanceMeters;
                transform.position = hit.point + continueVector;
                _traveledDistanceMeters += continueDistanceMeters;

                if (_traveledDistanceMeters >= _runtimeData.MaxTravelDistanceMeters)
                {
                    Destroy(gameObject);
                }

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

        /// <summary>
        /// true のとき、このフレーム内で弾を通過継続させる。
        /// false のとき、弾はここで止まる。
        /// </summary>
        private bool HandleHit(RaycastHit hit)
        {
            IBulletHittable hittable = hit.collider.GetComponentInParent<IBulletHittable>();
            if (hittable == null)
            {
                Destroy(gameObject);
                return false;
            }

            BulletHitContext context = _playerBullet.CreateHitContext(hit.point);
            BulletHitResponse response = hittable.ReceiveBulletHit(context);

            _playerBullet.ApplyHitResponse(response, hit.collider.gameObject);

            // ApplyHitResponse 内で Destroy された可能性がある
            if (_playerBullet == null || gameObject == null)
            {
                return false;
            }

            return response.BulletReaction switch
            {
                BulletHitReaction.None => true,
                BulletHitReaction.Pierce => true,
                _ => false,
            };
        }
    }
}
