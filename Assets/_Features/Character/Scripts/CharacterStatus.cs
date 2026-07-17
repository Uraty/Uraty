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

        [SerializeField]
        private Animator _animator;

        [SerializeField]
        private CharacterAudio _audio;

        [Header("Team")]
        [SerializeField]
        private TeamId _teamId = TeamId.None;

        [Header("Health")]
        [SerializeField]
        private float _maxHp = 100f;

        [Header("Recovery")]
        [Tooltip("攻撃・被弾後、回復し始めるまでの秒数")]
        [SerializeField]
        private float _recoveryStartDelaySeconds = 3f;

        [Tooltip("回復し始めたあとに何秒ごとに回復するか")]
        [SerializeField]
        private float _recoveryIntervalSeconds = 1f;

        [Tooltip("1回ごとの回復量。MaxHpに対する割合(%)")]
        [SerializeField]
        private float _recoveryAmountPercent = 10f;

        [Header("Reload")]
        [Tooltip("最大リロード数")]
        [SerializeField]
        private float _maxReloadCount = 3f;

        [Tooltip("毎秒回復するリロード数")]
        [SerializeField]
        private float _reloadRecoveryPerSecond = 1f;

        [SerializeField]
        private float _currentReloadCount;

        [SerializeField]
        private float _currentSuperChargePercent;

        private readonly Subject<CharacterStatus> _diedSubject = new();
        private readonly Subject<CharacterStatus> _killedSubject = new();
        private readonly Subject<CharacterDamageEvent> _damageReceivedSubject = new();
        private readonly Subject<CharacterDamageEvent> _damageDealtSubject = new();
        private readonly Subject<CharacterHealEvent> _healedSubject = new();

        private float _currentHp;
        private bool _isDead;
        private bool _isInsideBush;

        private float _recoveryElapsedSeconds;
        private float _nextRecoveryTimeSeconds;
        private bool _hasPlayedRecoveryHealSe;
        private bool _hasPlayedSuperHealSeForCurrentUse;

        private bool _canAttack = true;
        private float _attackDisableRemainingSeconds;

        public TeamId TeamId => _teamId;
        public float MaxHp => _maxHp;
        public float CurrentHp => _currentHp;
        public bool IsDead => _isDead;
        public bool IsAlive => !_isDead;
        public bool IsInsideBush => _isInsideBush;
        public Animator Animator => _animator;
        public float MaxReloadCount => _maxReloadCount;
        public float CurrentReloadCount => _currentReloadCount;
        public float ReloadRecoveryPerSecond => _reloadRecoveryPerSecond;

        public float CurrentSuperChargePercent => _currentSuperChargePercent;
        public bool IsSuperReady => _currentSuperChargePercent >= MaxSuperChargePercent;

        public Observable<CharacterStatus> DiedStream => _diedSubject;
        public Observable<CharacterStatus> KilledStream => _killedSubject;
        public Observable<CharacterDamageEvent> DamageReceivedStream => _damageReceivedSubject;
        public Observable<CharacterDamageEvent> DamageDealtStream => _damageDealtSubject;
        public Observable<CharacterHealEvent> HealedStream => _healedSubject;

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
            if (_audio == null)
            {
                TryGetComponent(out _audio);
            }

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
            _damageReceivedSubject.Dispose();
            _damageDealtSubject.Dispose();
            _healedSubject.Dispose();
        }

        public void Initialize(TeamId teamId)
        {
            Initialize(
                teamId,
                0);
        }

        public void Initialize(
            TeamId teamId,
            int roleTypeValue)
        {
            _teamId = teamId;

            if (_audio != null)
            {
                _audio.Initialize(roleTypeValue);
            }

            ResetHealth();
        }

        public void Respawn()
        {
            ResetHealth();
            _audio?.PlayRespawn();
        }

        public void SetInsideBush(bool isInsideBush)
        {
            _isInsideBush = isInsideBush;
        }

        public void NotifyAttackBulletSpawned()
        {
            _audio?.PlayAttack();
        }

        public void NotifySuperBulletSpawned()
        {
            _audio?.PlaySuper();
        }

        public bool TryBeginAttack(float attackDisableSeconds)
        {
            if (_isDead || !_canAttack)
            {
                return false;
            }

            if (_currentReloadCount < AttackReloadCost)
            {
                _audio?.PlayNoAmmo();
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

            _hasPlayedSuperHealSeForCurrentUse = false;

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

            bool wasSuperReady = IsSuperReady;

            _currentSuperChargePercent =
                Mathf.Min(
                    MaxSuperChargePercent,
                    _currentSuperChargePercent + validPercent);

            if (!wasSuperReady && IsSuperReady)
            {
                _audio?.PlaySuperReady();
            }
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
            float actualHealAmount =
                ApplyHeal(amount);

            if (actualHealAmount > 0f)
            {
                _audio?.PlayHeal();
            }
        }

        public void HealFromSuper(float amount)
        {
            float actualHealAmount =
                ApplyHeal(amount);

            if (actualHealAmount <= 0f
                || _hasPlayedSuperHealSeForCurrentUse)
            {
                return;
            }

            _hasPlayedSuperHealSeForCurrentUse = true;
            _audio?.PlaySuperHeal();
        }

        private float ApplyHeal(float amount)
        {
            if (_isDead)
            {
                return 0f;
            }

            float validAmount = Mathf.Max(0f, amount);

            if (validAmount <= 0f)
            {
                return 0f;
            }

            float previousHp = _currentHp;

            _currentHp = Mathf.Min(_maxHp, _currentHp + validAmount);

            float actualHealAmount =
                Mathf.Max(
                    0f,
                    _currentHp - previousHp);

            if (actualHealAmount <= 0f)
            {
                return 0f;
            }

            _healedSubject.OnNext(
                new CharacterHealEvent(
                    this,
                    actualHealAmount));

            return actualHealAmount;
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

            CharacterStatus attackerStatus =
                ResolveAttackerStatus(attackerObject);

            float previousHp = _currentHp;

            _currentHp = Mathf.Max(0f, _currentHp - validDamage);

            float actualDamageAmount =
                Mathf.Max(
                    0f,
                    previousHp - _currentHp);

            if (actualDamageAmount > 0f)
            {
                PublishDamageEvent(
                    attackerStatus,
                    actualDamageAmount);
            }

            if (_currentHp <= 0f)
            {
                Die(
                    attackerObject);
            }
        }

        private CharacterStatus ResolveAttackerStatus(GameObject attackerObject)
        {
            if (attackerObject == null)
            {
                return null;
            }

            if (!attackerObject.TryGetComponent(out CharacterStatus attackerStatus))
            {
                return null;
            }

            if (attackerStatus == this)
            {
                return null;
            }

            return attackerStatus;
        }

        private void PublishDamageEvent(
            CharacterStatus attackerStatus,
            float actualDamageAmount)
        {
            CharacterDamageEvent damageEvent =
                new CharacterDamageEvent(
                    attackerStatus,
                    this,
                    actualDamageAmount);

            _damageReceivedSubject.OnNext(
                damageEvent);

            if (attackerStatus == null)
            {
                return;
            }

            attackerStatus.PublishDamageDealt(
                damageEvent);
        }

        private void PublishDamageDealt(CharacterDamageEvent damageEvent)
        {
            _damageDealtSubject.OnNext(
                damageEvent);
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

            float previousReloadCount =
                _currentReloadCount;

            _currentReloadCount =
                Mathf.Min(
                    _maxReloadCount,
                    _currentReloadCount
                    + _reloadRecoveryPerSecond * Time.deltaTime);

            int previousCompletedReloadCount =
                Mathf.FloorToInt(previousReloadCount);

            int currentCompletedReloadCount =
                Mathf.FloorToInt(_currentReloadCount);

            for (int completedReloadCount = previousCompletedReloadCount;
                 completedReloadCount < currentCompletedReloadCount;
                 completedReloadCount++)
            {
                _audio?.PlayReload();
            }
        }

        private void HealByRecoveryPercent()
        {
            float healAmount =
                _maxHp *
                (_recoveryAmountPercent / 100f);

            float actualHealAmount =
                ApplyHeal(healAmount);

            if (actualHealAmount <= 0f
                || _hasPlayedRecoveryHealSe)
            {
                return;
            }

            _hasPlayedRecoveryHealSe = true;
            _audio?.PlayHeal();
        }

        private void InterruptRecovery()
        {
            _recoveryElapsedSeconds = 0f;
            _nextRecoveryTimeSeconds = _recoveryStartDelaySeconds;
            _hasPlayedRecoveryHealSe = false;
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

            _audio?.PlayDead();

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
