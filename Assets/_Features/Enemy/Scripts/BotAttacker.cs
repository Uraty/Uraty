using UnityEngine;

namespace Uraty.Feature.Enemy
{
    public class BotAttacker : MonoBehaviour
    {
        // 攻撃（発射）間隔（秒）
        [SerializeField] private float _attackIntervalSeconds = 2f;

        // 弾の初速（Rigidbody に与える速度）
        [SerializeField] private float _bulletSpeed = 10f;

        // 発射する弾のプレハブ
        [SerializeField] private GameObject _bulletPrefab;

        // 弾の生成位置（銃口など）
        [SerializeField] private Transform _firePoint;

        // 前回の発射からの経過時間を蓄積するタイマー
        private float _attackTimer;

        /// <summary>
        /// 毎フレーム呼び出して、攻撃間隔を満たしたら弾を発射します。
        /// </summary>
        /// <param name="target">狙う対象（プレイヤーなど）</param>
        public void TickAttack(Transform target)
        {
            // 対象がいなければ何もしない
            if (target == null)
            {
                return;
            }

            // 経過時間を加算
            _attackTimer += Time.deltaTime;

            //まだ攻撃間隔に達していない場合は待機
            if (_attackTimer < _attackIntervalSeconds)
            {
                return;
            }

            // タイマーをリセットして発射
            _attackTimer = 0f;
            Fire(target);
        }

        /// <summary>
        /// ターゲット方向に弾を生成し、Rigidbody に速度を与えて飛ばします。
        /// </summary>
        private void Fire(Transform target)
        {
            // Inspectorでの参照が未設定ならエラーを出して中断
            if (_bulletPrefab == null || _firePoint == null)
            {
                Debug.LogError("BulletPrefab or FirePoint is not assigned");
                return;
            }

            // 発射点からターゲットへの方向ベクトル（正規化）
            var direction = (target.position - _firePoint.position).normalized;

            //方向に向けて弾を生成（弾の前方をターゲット方向に合わせる）
            var bullet = Instantiate(
                _bulletPrefab,
                _firePoint.position,
                Quaternion.LookRotation(direction)
            );

            // Rigidbody がある場合のみ、速度を与えて飛ばす（無い場合は見た目だけ生成される）
            var rigidbody = bullet.GetComponent<Rigidbody>();

            if (rigidbody != null)
            {
                rigidbody.linearVelocity = direction * _bulletSpeed;
            }
        }
    }
}
