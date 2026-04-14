using UnityEngine;

namespace Uraty.Feature.Enemy
{
    public class BotController : MonoBehaviour
    {
        // 周囲のプレイヤーを探索する（最も近いプレイヤーを返す）
        private BotDetector _detector;

        // 視線が通っているか（障害物で遮られていないか）を判定する
        private BotVision _vision;

        // 一定間隔で弾を発射する
        private BotAttacker _attacker;

        // 現在狙っているターゲット（最も近いプレイヤー）
        private Transform _currentTarget;

        private void Awake()
        {
            // Inspectorで参照が設定されていない場合は同一GameObjectから取得する
            if (_detector == null)
            {
                _detector = GetComponent<BotDetector>();
            }

            if (_vision == null)
            {
                _vision = GetComponent<BotVision>();
            }

            if (_attacker == null)
            {
                _attacker = GetComponent<BotAttacker>();
            }
        }

        private void Update()
        {
            // 毎フレーム、ターゲット更新 → 攻撃更新 の順に実行
            UpdateTarget();
            UpdateAttack();
        }

        private void UpdateTarget()
        {
            // 検知範囲内の「最も近い Player」をターゲットにする
            _currentTarget = _detector.GetNearestPlayer();
        }

        private void UpdateAttack()
        {
            // ターゲットがいなければ攻撃しない
            if (_currentTarget == null)
            {
                return;
            }

            // 障害物で見えていない場合は攻撃しない
            if (!_vision.CanSeeTarget(_currentTarget))
            {
                return;
            }

            // ターゲット方向に向きを合わせる（回転）
            transform.LookAt(_currentTarget);

            // 攻撃（発射）は Attacker 側で間隔管理する
            _attacker.TickAttack(_currentTarget);
        }
    }
}
