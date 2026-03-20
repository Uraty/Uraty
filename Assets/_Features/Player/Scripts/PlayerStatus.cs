using UnityEngine;

namespace Uraty.Feature.Player
{
    public class PlayerStatus : MonoBehaviour
    {
        [SerializeField] private RoleDefinition _roleDefinition;
        [SerializeField, Min(0.01f)] private float _hpRegenStartDelaySeconds = 5f;
        [SerializeField, Min(0.01f)] private float _hpRegenIntervalSeconds = 1f;
        [SerializeField, Min(0.01f)] private float _hpRegenPercentPerInterval = 2f;
        [SerializeField, Min(0.01f)] private float _respawnTimeSeconds = 5f;

        private bool _isHpRegenActive = false;
        private float _hpRegenStartDelayElapsedSeconds = 0f;
        private float _hpRegenIntervalElapsedSeconds = 0f;

        private float _currentHp;
        private float _currentAmmo;
        private bool _isDead = false;
        private float _respawnTimeRemainingSeconds = 0f;

        public RoleDefinition RoleDefinition => _roleDefinition;
        public float CurrentHp => _currentHp;
        public float CurrentAmmo => _currentAmmo;
        public bool IsDead => _isDead;
        public float RespawnTimeRemainingSeconds => _respawnTimeRemainingSeconds;

        private void Start()
        {
            if (!_roleDefinition)
            {
                return;
            }

            _currentHp = _roleDefinition.MaxHp;
            _currentAmmo = _roleDefinition.MaxAmmo;
            ResetHpRegen();
        }

        private void FixedUpdate()
        {
            if (!_roleDefinition)
            {
                return;
            }

            if (_isDead)
            {
                UpdateRespawn();
                return;
            }

            UpdateHpRegen();

            ApplyAmmoRecover();
        }

        public void ApplyDamage(float damageAmount)
        {
            if (!_roleDefinition || _isDead || damageAmount <= 0f)
            {
                return;
            }

            _currentHp = Mathf.Max(_currentHp - damageAmount, 0f);
            ResetHpRegen();

            if (_currentHp <= 0f)
            {
                Die();
            }
        }

        public bool TryConsumeAmmo(float consumeCount)
        {
            if (!_roleDefinition || _isDead || consumeCount <= 0f)
            {
                return false;
            }

            if (_currentAmmo < consumeCount)
            {
                return false;
            }

            _currentAmmo -= consumeCount;
            ResetHpRegen();

            return true;
        }

        private void UpdateHpRegen()
        {
            if (_currentHp >= _roleDefinition.MaxHp)
            {
                _currentHp = _roleDefinition.MaxHp;
                ResetHpRegen();
                return;
            }

            if (!_isHpRegenActive)
            {
                _hpRegenStartDelayElapsedSeconds += Time.fixedDeltaTime;

                if (_hpRegenStartDelayElapsedSeconds < _hpRegenStartDelaySeconds)
                {
                    return;
                }

                _isHpRegenActive = true;
                _hpRegenIntervalElapsedSeconds = 0f;
                ApplyHpRegen();
                return;
            }

            _hpRegenIntervalElapsedSeconds += Time.fixedDeltaTime;

            while (_hpRegenIntervalElapsedSeconds >= _hpRegenIntervalSeconds)
            {
                _hpRegenIntervalElapsedSeconds -= _hpRegenIntervalSeconds;
                ApplyHpRegen();

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
            if (_currentHp >= _roleDefinition.MaxHp)
            {
                return;
            }

            float regenAmount = _roleDefinition.MaxHp * (_hpRegenPercentPerInterval / 100f);
            _currentHp = Mathf.Min(_currentHp + regenAmount, _roleDefinition.MaxHp);
        }

        private void ApplyAmmoRecover()
        {
            if (_currentAmmo >= _roleDefinition.MaxAmmo)
            {
                return;
            }

            _currentAmmo += Time.fixedDeltaTime / _roleDefinition.AmmoRecoverIntervalSeconds;
            _currentAmmo = Mathf.Min(_currentAmmo, _roleDefinition.MaxAmmo);
        }

        private void ResetHpRegen()
        {
            _isHpRegenActive = false;
            _hpRegenStartDelayElapsedSeconds = 0f;
            _hpRegenIntervalElapsedSeconds = 0f;
        }

        private void Die()
        {
            _isDead = true;
            _currentHp = 0f;
            _respawnTimeRemainingSeconds = _respawnTimeSeconds;
            ResetHpRegen();
        }

        private void UpdateRespawn()
        {
            _respawnTimeRemainingSeconds -= Time.fixedDeltaTime;

            if (_respawnTimeRemainingSeconds > 0f)
            {
                return;
            }

            Respawn();
        }

        private void Respawn()
        {
            _isDead = false;
            _currentHp = _roleDefinition.MaxHp;
            _currentAmmo = _roleDefinition.MaxAmmo;
            _respawnTimeRemainingSeconds = 0f;
            ResetHpRegen();
        }
    }
}
