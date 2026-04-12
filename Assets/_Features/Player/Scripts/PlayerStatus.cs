using UnityEngine;

namespace Uraty.Feature.Player
{
    public class PlayerStatus : MonoBehaviour
    {
        // プレイヤーの役職ごとの設定データ
        [SerializeField] private RoleDefinition _roleDefinition;

        // HP自動回復が始まるまでの待機時間
        [SerializeField, Min(0.01f)] private float _hpRegenStartDelaySeconds = 5f;

        // HP自動回復の実行間隔
        [SerializeField, Min(0.01f)] private float _hpRegenIntervalSeconds = 1f;

        // 1回あたりのHP回復量（最大HPに対する割合）
        [SerializeField, Min(0.01f)] private float _hpRegenPercentPerInterval = 2f;

        // リスポーンまでの待機時間
        [SerializeField, Min(0.01f)] private float _respawnTimeSeconds = 5f;

        // 現在保持しているスコア
        [SerializeField, Min(0)] private int _currentScore = 0;

        // HP自動回復が有効かどうか
        private bool _isHpRegenActive = false;

        // HP自動回復開始までの経過時間
        private float _hpRegenStartDelayElapsedSeconds = 0f;

        // HP自動回復の間隔管理用経過時間
        private float _hpRegenIntervalElapsedSeconds = 0f;

        // 現在のHP
        private float _currentHp;

        // 現在の弾数
        private float _currentAmmo;

        // 死亡状態かどうか
        private bool _isDead = false;

        // リスポーンまでの残り時間
        private float _respawnTimeRemainingSeconds = 0f;


        // 外部参照用プロパティ
        public RoleDefinition RoleDefinition => _roleDefinition;
        public float CurrentHp => _currentHp;
        public float CurrentAmmo => _currentAmmo;
        public bool IsDead => _isDead;
        public float RespawnTimeRemainingSeconds => _respawnTimeRemainingSeconds;

        private void Start()
        {
            // ロール設定が存在しない場合は初期化しない
            if (!_roleDefinition)
            {
                return;
            }

            // HPと弾数を最大値で初期化
            _currentHp = _roleDefinition.MaxHp;
            _currentAmmo = _roleDefinition.MaxAmmo;

            // HP自動回復状態を初期化
            ResetHpRegen();
        }

        private void FixedUpdate()
        {
            // ロール設定がなければ何もしない
            if (!_roleDefinition)
            {
                return;
            }

            // 死亡中はリスポーン処理のみ実行
            if (_isDead)
            {
                UpdateRespawn();
                return;
            }

            // HP自動回復処理
            UpdateHpRegen();

            // 弾数自動回復処理
            ApplyAmmoRecover();
        }

        public void ApplyDamage(float damageAmount)
        {
            // 無効状態、死亡中、ダメージ量が不正なら処理しない
            if (!_roleDefinition || _isDead || damageAmount <= 0f)
            {
                return;
            }

            // HPを減少させる
            _currentHp = Mathf.Max(_currentHp - damageAmount, 0f);

            // ダメージを受けたのでHP回復をリセット
            ResetHpRegen();

            // HPが0以下になったら死亡処理
            if (_currentHp <= 0f)
            {
                Die();
            }
        }

        public bool TryConsumeAmmo(float consumeCount)
        {
            // 無効状態、死亡中、消費量が不正なら失敗
            if (!_roleDefinition || _isDead || consumeCount <= 0f)
            {
                return false;
            }

            // 弾数不足なら失敗
            if (_currentAmmo < consumeCount)
            {
                return false;
            }

            // 弾数を消費
            _currentAmmo -= consumeCount;

            // 行動が発生したのでHP回復をリセット
            ResetHpRegen();

            return true;
        }

        private void UpdateHpRegen()
        {
            // 既に最大HPなら値を補正して回復状態をリセット
            if (_currentHp >= _roleDefinition.MaxHp)
            {
                _currentHp = _roleDefinition.MaxHp;
                ResetHpRegen();
                return;
            }

            // まだ回復開始前なら待機時間を進める
            if (!_isHpRegenActive)
            {
                _hpRegenStartDelayElapsedSeconds += Time.fixedDeltaTime;

                // 待機時間が足りなければまだ回復しない
                if (_hpRegenStartDelayElapsedSeconds < _hpRegenStartDelaySeconds)
                {
                    return;
                }

                // 回復開始
                _isHpRegenActive = true;
                _hpRegenIntervalElapsedSeconds = 0f;

                // 開始直後に1回回復
                ApplyHpRegen();
                return;
            }

            // 回復中なら回復間隔を進める
            _hpRegenIntervalElapsedSeconds += Time.fixedDeltaTime;

            // 指定間隔ごとに回復を行う
            while (_hpRegenIntervalElapsedSeconds >= _hpRegenIntervalSeconds)
            {
                _hpRegenIntervalElapsedSeconds -= _hpRegenIntervalSeconds;
                ApplyHpRegen();

                // 最大HPに達したら補正して終了
                if (_currentHp >= _roleDefinition.MaxHp)
                {
                    _currentHp = _roleDefinition.MaxHp;
                    ResetHpRegen();
                    break;
                }
            }
        }

        private void ApplyHpRegen()
        {
            // 既に最大HPなら何もしない
            if (_currentHp >= _roleDefinition.MaxHp)
            {
                return;
            }

            // 最大HPに対する割合で回復量を計算
            float regenAmount = _roleDefinition.MaxHp * (_hpRegenPercentPerInterval / 100f);

            // 最大HPを超えないように回復
            _currentHp = Mathf.Min(_currentHp + regenAmount, _roleDefinition.MaxHp);
        }

        private void ApplyAmmoRecover()
        {
            // 既に最大弾数なら何もしない
            if (_currentAmmo >= _roleDefinition.MaxAmmo)
            {
                return;
            }

            // リロード間隔に応じて徐々に弾数を回復
            _currentAmmo += Time.fixedDeltaTime / _roleDefinition.ReloadIntervalSeconds;
            _currentAmmo = Mathf.Min(_currentAmmo, _roleDefinition.MaxAmmo);
        }

        private void ResetHpRegen()
        {
            // HP自動回復状態を初期化
            _isHpRegenActive = false;
            _hpRegenStartDelayElapsedSeconds = 0f;
            _hpRegenIntervalElapsedSeconds = 0f;
        }

        private void Die()
        {
            // 死亡状態へ移行
            _isDead = true;
            _currentHp = 0f;

            // リスポーン待機時間を設定
            _respawnTimeRemainingSeconds = _respawnTimeSeconds;

            // HP回復状態をリセット
            ResetHpRegen();
        }

        private void UpdateRespawn()
        {
            // 残り時間を減らす
            _respawnTimeRemainingSeconds -= Time.fixedDeltaTime;

            // まだ時間が残っていれば待機
            if (_respawnTimeRemainingSeconds > 0f)
            {
                return;
            }

            // 時間が来たら復活
            Respawn();
        }

        private void Respawn()
        {
            // 死亡状態を解除
            _isDead = false;

            // HPと弾数を最大値に戻す
            _currentHp = _roleDefinition.MaxHp;
            _currentAmmo = _roleDefinition.MaxAmmo;

            // リスポーン残り時間をリセット
            _respawnTimeRemainingSeconds = 0f;

            // HP回復状態を初期化
            ResetHpRegen();
        }

        // 指定されたスコアを現在の保持スコアに加算する
        public void AddScore(int scoreAmount)
        {
            // 0以下のスコアは加算しない
            if (scoreAmount <= 0)
            {
                return;
            }

            _currentScore += scoreAmount;
        }

        // 現在保持しているスコアを返し、保持値を0にリセットする
        public int TransferScore()
        {
            int score = _currentScore;
            _currentScore = 0;
            return score;
        }
    }
}
