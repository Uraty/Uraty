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

        private float _currentHp;
        private bool _isDead;
        private bool _isInsideBush;

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

        public void Initialize(TeamId teamId)
        {
            _teamId = teamId;
            ResetHealth();
        }

        public void SetInsideBush(bool isInsideBush)
        {
            _isInsideBush = isInsideBush;
        }

        public bool ReceiveBulletHit(
            GameObject owner,
            TeamId teamId,
            float damage,
            bool isPiercing)
        {
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

        private void ResetHealth()
        {
            _maxHp = Mathf.Max(1f, _maxHp);
            _currentHp = _maxHp;
            _isDead = false;
            _isInsideBush = false;
        }

        private void Die()
        {
            _isDead = true;
            _currentHp = 0f;

            gameObject.SetActive(false);
        }
    }
}
