using UnityEngine;
using Uraty.Shared.Terrain;

namespace Uraty.Feature.Player
{
    /// <summary>
    /// Player の移動先が通行可能かを判定し、最終的な移動量を解決する。
    /// </summary>
    public sealed class PlayerTerrainMoveResolver : MonoBehaviour
    {
        private const int MaxOverlapColliderCount = 16;
        private const float DefaultCollisionRadiusMeters = 0.35f;

        [Header("通行判定に使う半径")]
        [SerializeField] private float _collisionRadiusMeters = DefaultCollisionRadiusMeters;

        [Header("地形判定に使う LayerMask")]
        [SerializeField] private LayerMask _terrainLayerMask = Physics.AllLayers;

        private readonly Collider[] _overlapResults = new Collider[MaxOverlapColliderCount];

        private void Awake()
        {
            if (_collisionRadiusMeters < 0.0f)
            {
                _collisionRadiusMeters = 0.0f;
            }
        }

        public Vector3 ResolveMoveDelta(Vector3 currentPosition, Vector3 desiredMoveDelta)
        {
            Vector3 resolvedMoveDelta = Vector3.zero;

            // X 軸だけ先に判定して、斜め入力時に壁沿いへ滑れるようにする。
            var xMoveDelta = new Vector3(desiredMoveDelta.x, 0.0f, 0.0f);
            resolvedMoveDelta += ResolveAxisMove(currentPosition + resolvedMoveDelta, xMoveDelta);

            // Z 軸も同様に個別判定する。
            var zMoveDelta = new Vector3(0.0f, 0.0f, desiredMoveDelta.z);
            resolvedMoveDelta += ResolveAxisMove(currentPosition + resolvedMoveDelta, zMoveDelta);

            return resolvedMoveDelta;
        }

        private Vector3 ResolveAxisMove(Vector3 currentPosition, Vector3 axisMoveDelta)
        {
            if (axisMoveDelta.sqrMagnitude <= 0.0f)
            {
                return Vector3.zero;
            }

            Vector3 targetPosition = currentPosition + axisMoveDelta;
            return CanPass(targetPosition) ? axisMoveDelta : Vector3.zero;
        }

        private bool CanPass(Vector3 targetPosition)
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(
                targetPosition,
                _collisionRadiusMeters,
                _overlapResults,
                _terrainLayerMask,
                QueryTriggerInteraction.Collide);

            var passContext = new PlayerPassContext(transform, targetPosition);

            for (int i = 0; i < overlapCount; i++)
            {
                Collider hitCollider = _overlapResults[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                IPlayerPassRule passRule = hitCollider.GetComponentInParent<IPlayerPassRule>();
                if (passRule == null)
                {
                    continue;
                }

                if (!passRule.CanPlayerPass(passContext))
                {
                    return false;
                }
            }

            return true;
        }
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, _collisionRadiusMeters);
        }
#endif
    }
}
