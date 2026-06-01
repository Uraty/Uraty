using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Health
{
    public class HPSystem : MonoBehaviour
    {
        [SerializeField] private BarBase _barCurrentHP;
        [SerializeField] private BarBase _barDamage;

        [SerializeField] private float _maxHP = 1000.0f;
        [SerializeField] private float _currentHP = 1000.0f;
        [SerializeField] private float _debugDamageValue = 100.0f;
        [SerializeField] private float _damageRatioDownSpeed = 0.002f;

        private bool _isDamage = false;

        public float MaxHP => _maxHP;
        public float CurrentHP => _currentHP;
        public bool IsDamage => _isDamage;


        private void Start()
        {
            float hpRatio = Mathf.Clamp01(_currentHP / _maxHP);

            _barCurrentHP.SetBarRatio(hpRatio);
            _barDamage.SetBarRatio(hpRatio);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                DamageHP(_debugDamageValue);
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                HealHP(_debugDamageValue);
            }

            if (!_isDamage)
            {
                return;
            }

            float currentRatio = _barCurrentHP.BarRatio;
            float damageRatio = _barDamage.BarRatio;

            damageRatio = Mathf.MoveTowards(
                damageRatio,
                currentRatio,
                _damageRatioDownSpeed
            );

            _barDamage.SetBarRatio(damageRatio);

            if (Mathf.Approximately(damageRatio, currentRatio))
            {
                _isDamage = false;
            }
        }

        public void SetMaxHP(float maxHP)
        {
            _maxHP = Mathf.Max(1.0f, maxHP);

            float hpRatio = Mathf.Clamp01(_currentHP / _maxHP);

            _barCurrentHP.SetBarRatio(hpRatio);
            _barDamage.SetBarRatio(hpRatio);
        }

        public void HealHP(float healValue)
        {
            _currentHP = Mathf.Clamp(_currentHP + healValue, 0.0f, _maxHP);

            float hpRatio = Mathf.Clamp01(_currentHP / _maxHP);

            _barCurrentHP.SetBarRatio(hpRatio);
            _barDamage.SetBarRatio(hpRatio);

            _isDamage = false;
        }

        public void DamageHP(float damageValue)
        {
            _currentHP = Mathf.Clamp(_currentHP - damageValue, 0.0f, _maxHP);
            if(_currentHP == 0.0f)
            {
                _barCurrentHP.SetBarRatio(0.0f);
                _barDamage.SetBarRatio(0.0f);
                return;
            }
            float hpRatio = Mathf.Clamp01(_currentHP / _maxHP);
            _barCurrentHP.SetBarRatio(hpRatio);

            _isDamage = true;
        }
    }
}
