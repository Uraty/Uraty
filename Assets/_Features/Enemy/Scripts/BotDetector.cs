using UnityEngine;

namespace Uraty.Feature.Enemy
{
    public class BotDetector : MonoBehaviour
    {
        // プレイヤーを検知する半径（OverlapSphere の範囲）
        [SerializeField] private float _detectionRadius = 10f;

        /// <summary>
        /// 検知範囲内の Collider から、最も近い "Player" タグの Transform を返します。
        /// </summary>
        public Transform GetNearestPlayer()
        {
            // 指定半径内にある Collider を取得
            var colliders = Physics.OverlapSphere(transform.position, _detectionRadius);

            Transform nearestTarget = null;
            var nearestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                // Player タグ以外は無視
                if (!collider.CompareTag("Player"))
                {
                    continue;
                }

                // 自分からの距離を計算
                var distance = Vector3.Distance(transform.position, collider.transform.position);

                // 最短距離のターゲットを更新
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTarget = collider.transform;
                }
            }

            return nearestTarget;
        }

        private void OnDrawGizmosSelected()
        {
            // Sceneビューで検知範囲を可視化（選択中のみ表示）
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        }
    }
}
