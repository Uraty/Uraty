using UnityEngine;

namespace Uraty.Feature.Enemy
{
    public class BotVision : MonoBehaviour
    {
        // 視線判定で「障害物」として扱うレイヤー（このレイヤーに当たったら見えていない）
        [SerializeField] private LayerMask _obstacleLayerMask;

        // 視線の起点（目の位置など）
        [SerializeField] private Transform _eyePoint;

        /// <summary>
        /// ターゲットまで障害物がない（＝Raycastが障害物に当たらない）場合に true。
        /// </summary>
        public bool CanSeeTarget(Transform target)
        {
            // 起点やターゲットが未設定なら見えない扱い
            if (_eyePoint == null || target == null)
            {
                return false;
            }

            var origin = _eyePoint.position;
            var direction = (target.position - origin).normalized;
            var distance = Vector3.Distance(origin, target.position);

            // 目からターゲット方向へレイを飛ばし、障害物レイヤーに当たったら視線が遮られている
            if (Physics.Raycast(origin, direction, out var hit, distance, _obstacleLayerMask))
            {
                return false;
            }

            return true;
        }
    }
}
