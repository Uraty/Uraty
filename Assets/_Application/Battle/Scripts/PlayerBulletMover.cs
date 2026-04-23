using UnityEngine;

using Uraty.Shared.Battle;

namespace Uraty.Application.Battle
{
    /// <summary>
    /// 生成済みの弾をフレームごとに移動させるクラス。
    ///
    /// このクラスの責務は以下。
    /// ・弾の前進
    /// ・射程管理
    /// ・ヒット検知
    /// ・ヒット対象への反応要求
    /// ・貫通可否に応じた移動継続
    /// </summary>
    [RequireComponent(typeof(PlayerBullet))]
    public sealed class PlayerBulletMover : MonoBehaviour
    {
        private const float MinMoveDistanceMeters = 0.0001f;
        private const float PassThroughEpsilonMeters = 0.01f;

        [Header("Hit Detection")]
        [SerializeField] private float _hitRadiusMeters = 0.1f;

        private BulletRuntimeData _runtimeData;
        private PlayerBullet _playerBullet;
        private float _traveledDistanceMeters;
        private bool _isInitialized;

        private void Awake()
        {
            _playerBullet = GetComponent<PlayerBullet>();
            _hitRadiusMeters = Mathf.Max(0f, _hitRadiusMeters);
        }

        /// <summary>
        /// 弾の実行時データを受け取って初期化する。
        /// </summary>
        public void Initialize(BulletRuntimeData runtimeData)
        {
            _runtimeData = runtimeData;
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

        /// <summary>
        /// 1フレーム分の移動処理を行う。
        /// </summary>
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

            if (!TryDetectHit(moveDirection, actualMoveDistanceMeters, out RaycastHit hit))
            {
                transform.position += moveVector;
                _traveledDistanceMeters += actualMoveDistanceMeters;

                if (_traveledDistanceMeters >= _runtimeData.MaxTravelDistanceMeters)
                {
                    Destroy(gameObject);
                }

                return;
            }

            float traveledToHitMeters = Mathf.Clamp(
                hit.distance,
                0f,
                actualMoveDistanceMeters);

            bool canContinue = HandleHit(hit);
            if (!canContinue)
            {
                return;
            }

            float remainingMoveAfterHitMeters =
                Mathf.Max(0f, actualMoveDistanceMeters - traveledToHitMeters);

            float traveledAfterHitMeters = remainingMoveAfterHitMeters;
            _traveledDistanceMeters += traveledToHitMeters + traveledAfterHitMeters;

            float separationDistanceMeters = Mathf.Min(
                PassThroughEpsilonMeters,
                remainingMoveAfterHitMeters);

            Vector3 nextPosition =
                hit.point +
                (moveDirection * traveledAfterHitMeters) +
                (moveDirection * separationDistanceMeters);

            transform.position = nextPosition;

            if (_traveledDistanceMeters >= _runtimeData.MaxTravelDistanceMeters)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 指定距離内にヒット対象があるかを SphereCast で調べる。
        /// </summary>
        private bool TryDetectHit(
            Vector3 moveDirection,
            float moveDistanceMeters,
            out RaycastHit hit)
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
            GameObject hitObject = hit.collider.gameObject;

            // 非マルチヒット時は、既に当たった対象への再ヒットを無視する。
            if (!_playerBullet.CanHitObject(hitObject))
            {
                return true;
            }

            BulletHitContext context = _playerBullet.CreateHitContext(hit.point);
            BulletHitResponse response = ReceiveBulletHit(hit, context);

            if (!response.WasHandled)
            {
                Destroy(gameObject);
                return false;
            }

            _playerBullet.ApplyHitResponse(response, hitObject);
            return response.CanPassThrough;
        }

        /// <summary>
        /// 衝突した対象へ弾ヒット通知を送る。
        /// </summary>
        private static BulletHitResponse ReceiveBulletHit(
            RaycastHit hit,
            BulletHitContext context)
        {
            IBulletHittable hittable =
                hit.collider.GetComponentInParent<IBulletHittable>();

            if (hittable == null)
            {
                return BulletHitResponse.None;
            }

            return hittable.ReceiveBulletHit(context);
        }
    }
}
