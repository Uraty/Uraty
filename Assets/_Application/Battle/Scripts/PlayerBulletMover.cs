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
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            float moveDistanceMeters = _runtimeData.SpeedMetersPerSecond * safeDeltaTime;
            if (moveDistanceMeters <= MinMoveDistanceMeters)
            {
                return;
            }

            Vector3 moveDirection = _runtimeData.Direction.normalized;
            if (moveDirection.sqrMagnitude <= 0f)
            {
                return;
            }

            float remainingTravelDistanceMeters =
                _runtimeData.MaxTravelDistanceMeters - _traveledDistanceMeters;
            if (remainingTravelDistanceMeters <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float actualMoveDistanceMeters = Mathf.Min(
                moveDistanceMeters,
                remainingTravelDistanceMeters);

            Vector3 moveVector = moveDirection * actualMoveDistanceMeters;

            if (TryDetectHit(moveDirection, actualMoveDistanceMeters, out RaycastHit hit))
            {
                bool canContinue = HandleHit(hit);
                if (!canContinue)
                {
                    return;
                }

                float traveledToHitMeters = Mathf.Clamp(
                    hit.distance,
                    0f,
                    actualMoveDistanceMeters);

                float remainingMoveAfterHitMeters =
                    Mathf.Max(0f, actualMoveDistanceMeters - traveledToHitMeters);

                // 射程加算に含める「実際に進んだ距離」
                float traveledAfterHitMeters = remainingMoveAfterHitMeters;

                // 位置補正用のごく小さい押し出し。
                // 射程には含めず、同一コライダーへの即時再ヒットだけ避ける。
                float separationDistanceMeters = Mathf.Min(
                    PassThroughEpsilonMeters,
                    remainingMoveAfterHitMeters);

                transform.position =
                    hit.point + (moveDirection * separationDistanceMeters);

                _traveledDistanceMeters += traveledToHitMeters + traveledAfterHitMeters;

                if (_traveledDistanceMeters >= _runtimeData.MaxTravelDistanceMeters)
                {
                    Destroy(gameObject);
                    return;
                }

                return;
            }

            transform.position += moveVector;
            _traveledDistanceMeters += actualMoveDistanceMeters;

            if (_traveledDistanceMeters >= _runtimeData.MaxTravelDistanceMeters)
            {
                Destroy(gameObject);
                return;
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
