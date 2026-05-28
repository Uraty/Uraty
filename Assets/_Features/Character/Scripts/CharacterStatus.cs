using R3;

using UnityEngine;

using Uraty.Shared.Hit;
using Uraty.Shared.Team;

namespace Uraty.Features.Character
{
    public sealed class CharacterStatus : MonoBehaviour, IBulletHittable
    {
        private const float AttackReloadCost = 1f;
        private const float MaxSuperChargePercent = 100f;

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

        [Header("Reload")]
        [Tooltip("最大リロード数")]
        [SerializeField, Min(0f)]
        private float _maxReloadCount = 3f;

        [Tooltip("毎秒回復するリロード数")]
        [SerializeField, Min(0f)]
        private float _reloadRecoveryPerSecond = 1f;

        [SerializeField]
        private float _currentReloadCount;

        [SerializeField]
        private float _currentSuperChargePercent;

        private readonly Subject<CharacterStatus> _diedSubject = new();
        private readonly Subject<CharacterStatus> _killedSubject = new();

        private float _currentHp;
        private bool _isDead;
        private bool _isInsideBush;

        private float _recoveryElapsedSeconds;
        private float _nextRecoveryTimeSeconds;

        private bool _canAttack = true;
        private float _attackDisableRemainingSeconds;

        public TeamId TeamId => _teamId;
        public float MaxHp => _maxHp;
        public float CurrentHp => _currentHp;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;
        public bool IsInsideBush => _isInsideBush;

        public float MaxReloadCount => _maxReloadCount;
        public float CurrentReloadCount => _currentReloadCount;
        public float ReloadRecoveryPerSecond => _reloadRecoveryPerSecond;

        public float CurrentSuperChargePercent => _currentSuperChargePercent;
        public bool IsSuperReady => _currentSuperChargePercent >= MaxSuperChargePercent;

        public Observable<CharacterStatus> DiedStream => _diedSubject;
        public Observable<CharacterStatus> KilledStream => _killedSubject;

        public bool CanAttack =>
            !_isDead &&
            _canAttack &&
            _currentReloadCount >= AttackReloadCost;

        public bool CanSuper =>
            !_isDead &&
            _canAttack &&
            IsSuperReady;

        private void Awake()
        {
            ResetHealth();
        }

        private void Update()
        {
            UpdateAttackDisable();
            UpdateRecovery();
            UpdateReload();
        }

        private void OnValidate()
        {
            _maxHp = Mathf.Max(1f, _maxHp);

            _recoveryStartDelaySeconds =
                Mathf.Max(0f, _recoveryStartDelaySeconds);

            _recoveryIntervalSeconds =
                Mathf.Max(0.01f, _recoveryIntervalSeconds);

            _recoveryAmountPercent =
                Mathf.Max(0f, _recoveryAmountPercent);

            _maxReloadCount =
                Mathf.Max(0f, _maxReloadCount);

            _reloadRecoveryPerSecond =
                Mathf.Max(0f, _reloadRecoveryPerSecond);
        }

        private void OnDestroy()
        {
            _diedSubject.Dispose();
            _killedSubject.Dispose();
        }

        public void Initialize(TeamId teamId)
        {
            _teamId = teamId;
            ResetHealth();
        }

        public void Respawn()
        {
            ResetHealth();
        }

        public void SetInsideBush(bool isInsideBush)
        {
            _isInsideBush = isInsideBush;
        }

        public bool TryBeginAttack(float attackDisableSeconds)
        {
            if (!CanAttack)
            {
                return false;
            }

            _currentReloadCount =
                Mathf.Max(
                    0f,
                    _currentReloadCount - AttackReloadCost);

            InterruptRecovery();
            DisableAttack(attackDisableSeconds);

            return true;
        }

        public bool TryBeginSuper(float attackDisableSeconds)
        {
            if (!CanSuper)
            {
                return false;
            }

            _currentSuperChargePercent =
                Mathf.Max(
                    0f,
                    _currentSuperChargePercent - MaxSuperChargePercent);

            InterruptRecovery();
            DisableAttack(attackDisableSeconds);

            return true;
        }

        public void AddSuperCharge(float percent)
        {
            if (_isDead)
            {
                return;
            }

            float validPercent = Mathf.Max(0f, percent);

            if (validPercent <= 0f)
            {
                return;
            }

            _currentSuperChargePercent =
                Mathf.Min(
                    MaxSuperChargePercent,
                    _currentSuperChargePercent + validPercent);
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

            ApplyDamage(
                damage,
                owner);

            // 貫通攻撃でない場合は弾を壊す
            return !isPiercing;
        }

        public void ApplyDamage(float damage)
        {
            ApplyDamage(
                damage,
                null);
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

        private void ApplyDamage(
            float damage,
            GameObject attackerObject)
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
                Die(
                    attackerObject);
            }
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

        private void UpdateReload()
        {
            if (_isDead)
            {
                return;
            }

            if (!_canAttack)
            {
                return;
            }

            if (_currentReloadCount >= _maxReloadCount)
            {
                return;
            }

            if (_reloadRecoveryPerSecond <= 0f)
            {
                return;
            }

            _currentReloadCount =
                Mathf.Min(
                    _maxReloadCount,
                    _currentReloadCount
                    + _reloadRecoveryPerSecond * Time.deltaTime);
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

        private void DisableAttack(float seconds)
        {
            float validSeconds = Mathf.Max(0f, seconds);

            if (validSeconds <= 0f)
            {
                _canAttack = true;
                _attackDisableRemainingSeconds = 0f;
                return;
            }

            _canAttack = false;
            _attackDisableRemainingSeconds = validSeconds;
        }

        private void UpdateAttackDisable()
        {
            if (_canAttack)
            {
                return;
            }

            _attackDisableRemainingSeconds -= Time.deltaTime;

            if (_attackDisableRemainingSeconds > 0f)
            {
                return;
            }

            _canAttack = true;
            _attackDisableRemainingSeconds = 0f;
        }

        private void ResetHealth()
        {
            _maxHp = Mathf.Max(1f, _maxHp);
            _maxReloadCount = Mathf.Max(0f, _maxReloadCount);

            _currentHp = _maxHp;
            _currentReloadCount = _maxReloadCount;
            _currentSuperChargePercent = 0f;

            _isDead = false;
            _isInsideBush = false;

            _canAttack = true;
            _attackDisableRemainingSeconds = 0f;

            InterruptRecovery();
        }

        private void Die(GameObject killerObject)
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            _currentHp = 0f;

            _canAttack = false;
            _attackDisableRemainingSeconds = 0f;

            PublishKilledIfNeeded(
                killerObject);

            _diedSubject.OnNext(this);

            gameObject.SetActive(false);
        }

        private void PublishKilledIfNeeded(GameObject killerObject)
        {
            if (killerObject == null)
            {
                return;
            }

            if (!killerObject.TryGetComponent(out CharacterStatus killerStatus))
            {
                return;
            }

            if (killerStatus == this)
            {
                return;
            }

            if (killerStatus.TeamId == _teamId)
            {
                return;
            }

            _killedSubject.OnNext(killerStatus);
        }
    }
}
