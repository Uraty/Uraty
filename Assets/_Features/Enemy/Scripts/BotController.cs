using UnityEngine;

namespace Uraty.Feature.Enemy
{
    /// <summary>
    /// Bot のターゲット更新と攻撃実行を制御するクラス。
    ///
    /// このクラスは
    /// ・Detector によるターゲット取得
    /// ・Vision による視認判定
    /// ・Attacker への攻撃要求
    /// を仲介する。
    /// </summary>
    [RequireComponent(typeof(BotDetector))]
    [RequireComponent(typeof(BotVision))]
    [RequireComponent(typeof(BotAttacker))]
    public sealed class BotController : MonoBehaviour
    {
        // 周囲のターゲットを探索する。
        private BotDetector _detector;

        // ターゲットが視認可能かを判定する。
        private BotVision _vision;

        // 攻撃要求を処理する。
        private BotAttacker _attacker;

        // 現在狙っているターゲット。
        private Transform _currentTarget;

        private void Awake()
        {
            // 同一 GameObject 上の必須コンポーネントを取得する。
            _detector = GetComponent<BotDetector>();
            _vision = GetComponent<BotVision>();
            _attacker = GetComponent<BotAttacker>();
        }

        private void Update()
        {
            // 念のため必須参照が欠けている場合は何もしない。
            if (_detector == null)
            {
                return;
            }

            if (_vision == null)
            {
                return;
            }

            if (_attacker == null)
            {
                return;
            }

            // 毎フレーム、ターゲット更新 → 攻撃更新 の順に実行する。
            UpdateTarget();
            UpdateAttack();
        }

        /// <summary>
        /// 現在のターゲットを更新する。
        /// </summary>
        private void UpdateTarget()
        {
            _currentTarget = _detector.GetNearestPlayer();
        }

        /// <summary>
        /// 現在ターゲットに対して攻撃可能なら攻撃処理を進める。
        /// </summary>
        private void UpdateAttack()
        {
            // ターゲットがいなければ攻撃しない。
            if (_currentTarget == null)
            {
                return;
            }

            // 障害物などでターゲットが見えていない場合は攻撃しない。
            if (!_vision.CanSeeTarget(_currentTarget))
            {
                return;
            }

            RotateTowardsTargetOnPlane(_currentTarget);

            // 攻撃間隔の管理や実際の発射は Attacker 側に委譲する。
            _attacker.TickAttack(_currentTarget);
        }

        /// <summary>
        /// ターゲットの水平方向へ向きを合わせる。
        /// </summary>
        private void RotateTowardsTargetOnPlane(Transform target)
        {
            Vector3 lookDirection = target.position - transform.position;
            lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(lookDirection.normalized);
        }
    }
}
