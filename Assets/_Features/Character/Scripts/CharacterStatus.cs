using UnityEngine;

using Uraty.Shared.Team;
using Uraty.Shared.Hit;

namespace Uraty.Features.Character
{
    public sealed class CharacterStatus : MonoBehaviour, IBulletHittable
    {
        [Header("Team")]
        [SerializeField]
        private TeamId _teamId = TeamId.None;

        [Header("Health")]
        [Min(1f)]
        [SerializeField]
        private float _maxHp = 100f;

        [Header("Recovery")]
        [Tooltip("攻撃・被弾後、回復し始めるまでの秒数")]
        [SerializeField, Min(0f)]
        private float _recoveryStartDelaySeconds = 3f;

        [Tooltip("回復し始めたあとに何秒ごとに回復するか")]
        [SerializeField, Min(0.01f)]
        private float _recoveryIntervalSeconds = 1f;

        [Tooltip("1回ごとの回復量。MaxHpに対する割合(%)")]
        [SerializeField, Min(0f)]
        private float _recoveryAmountPercent = 10f;

        private float _currentHp;
        private bool _isDead;
        private bool _isInsideBush;

        private float _recoveryElapsedSeconds;
        private float _nextRecoveryTimeSeconds;

        public TeamId TeamId => _teamId;
        public float MaxHp => _maxHp;
        public float CurrentHp => _currentHp;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;
        public bool IsInsideBush => _isInsideBush;

        private void Awake()
        {
            ResetHealth();
        }

        private void Update()
        {
            UpdateRecovery();
        }

        private void OnValidate()
        {
            _maxHp = Mathf.Max(1f, _maxHp);
            _recoveryStartDelaySeconds = Mathf.Max(0f, _recoveryStartDelaySeconds);
            _recoveryIntervalSeconds = Mathf.Max(0.01f, _recoveryIntervalSeconds);
            _recoveryAmountPercent = Mathf.Max(0f, _recoveryAmountPercent);
        }

        public void Initialize(TeamId teamId)
        {
            _teamId = teamId;
            ResetHealth();
        }

        public void SetInsideBush(bool isInsideBush)
        {
            _isInsideBush = isInsideBush;
        }

        public void NotifyAttackPerformed()
        {
            InterruptRecovery();
        }

        public bool ReceiveBulletHit(
            GameObject owner,
            TeamId teamId,
            float damage,
            bool isPiercing)
        {
            if (_isDead)
            {
                return false;
            }

            if (_teamId == teamId)
            {
                return false;
            }

            ApplyDamage(damage);

            // 貫通攻撃でない場合は弾を壊す
            return !isPiercing;
        }

        public void ApplyDamage(float damage)
        {
            if (_isDead)
            {
                return;
            }

            float validDamage = Mathf.Max(0f, damage);
            if (validDamage <= 0f)
            {
                return;
            }

            InterruptRecovery();

            _currentHp = Mathf.Max(0f, _currentHp - validDamage);

            if (_currentHp <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (_isDead)
            {
                return;
            }

            float validAmount = Mathf.Max(0f, amount);
            if (validAmount <= 0f)
            {
                return;
            }

            _currentHp = Mathf.Min(_maxHp, _currentHp + validAmount);
        }

        private void UpdateRecovery()
        {
            if (_isDead)
            {
                return;
            }

            if (_currentHp >= _maxHp)
            {
                return;
            }

            if (_recoveryAmountPercent <= 0f)
            {
                return;
            }

            _recoveryElapsedSeconds += Time.deltaTime;

            while (_currentHp < _maxHp
                   && _recoveryElapsedSeconds >= _nextRecoveryTimeSeconds)
            {
                HealByRecoveryPercent();

                _nextRecoveryTimeSeconds += _recoveryIntervalSeconds;
            }
        }

        private void HealByRecoveryPercent()
        {
            float healAmount =
                _maxHp *
                (_recoveryAmountPercent / 100f);

            Heal(healAmount);
        }

        private void InterruptRecovery()
        {
            _recoveryElapsedSeconds = 0f;
            _nextRecoveryTimeSeconds = _recoveryStartDelaySeconds;
        }

        private void ResetHealth()
        {
            _maxHp = Mathf.Max(1f, _maxHp);
            _currentHp = _maxHp;
            _isDead = false;
            _isInsideBush = false;

            InterruptRecovery();
        }

        private void Die()
        {
            _isDead = true;
            _currentHp = 0f;

            gameObject.SetActive(false);
        }
    }
}
