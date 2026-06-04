using UnityEngine;
using TMPro;

namespace Uraty.Features.Character
{
    public sealed class HPSystem : MonoBehaviour
    {
        [SerializeField] private BarBase _barCurrentHP;
        [SerializeField] private BarBase _barDamage;
        [SerializeField] private TextMeshProUGUI _textCurrentHP;

        /**********************************************************************
        　　　　　　　　        2026/6/4 午前11:45分
                           被弾＆回復の演出が現在未実装
                     そのため定義と関連関数はコメントアウトしています。

        [SerializeField] private TMP_FontAsset _fontAsset;
        [Header("ダメージ用テキスト")]
        [SerializeField] private Color _colorTextDamage = Color.white;
        [SerializeField] private Color _colorOutlineDamage = Color.black;

        [Header("回復用テキスト")]
        [SerializeField] private Color _colorTextHeal = Color.white;
        [SerializeField] private Color _colorOutlineHeal = Color.black;

        ***********************************************************************/
        [SerializeField] private float _maxHP = 1000.0f;
        [SerializeField] private float _currentHP = 1000.0f;
        [SerializeField] private float _damageRatioDownSpeed = 0.002f;

        private bool _isDamage = false;

        public float MaxHP => _maxHP;
        public float CurrentHP => _currentHP;
        public bool IsDamage => _isDamage;

        private void Start()
        {
            float hpRatio = Mathf.Clamp01(_currentHP / _maxHP);

            SetMaxHP(_maxHP);
            SetTextCurrentHP(_maxHP);
            _barCurrentHP.SetBarRatio(hpRatio);
            _barDamage.SetBarRatio(hpRatio);
        }

        private void Update()
        {
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

        private void SetTextCurrentHP(float currentHP)
        {
            _textCurrentHP.text = currentHP.ToString();
        }

        /// <summary>
        /// キャラクターのHPの最大値をセット
        /// </summary>
        /// <param name="maxHP"></param>
        public void SetMaxHP(float maxHP)
        {
            _maxHP = Mathf.Max(1.0f, maxHP);

            float hpRatio = Mathf.Clamp01(_currentHP / _maxHP);

            _barCurrentHP.SetBarRatio(hpRatio);
            _barDamage.SetBarRatio(hpRatio);
        }

        /// <summary>
        /// 回復量を割合に変換してHPバーに反映
        /// </summary>
        /// <param name="healValue"></param>
        public void HealHP(float healValue)
        {
            _currentHP = Mathf.Clamp(_currentHP + healValue, 0.0f, _maxHP);
            SetTextCurrentHP(_currentHP);

            //
            //ShowTextHeal(healValue);
            //

            float hpRatio = Mathf.Clamp01(_currentHP / _maxHP);

            _barCurrentHP.SetBarRatio(hpRatio);
            _barDamage.SetBarRatio(hpRatio);

            _isDamage = false;
        }

        /// <summary>
        /// ダメージを割合に変換してHPバーに反映する
        /// </summary>
        /// <param name="damageValue"></param>
        public void DamageHP(float damageValue)
        {
            _currentHP = Mathf.Clamp(_currentHP - damageValue, 0.0f, _maxHP);
            SetTextCurrentHP(_currentHP);

            //
            //ShowTextDamage(damageValue);
            //

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

        /*************************************************************
         * 
        private void ShowTextDamage(float damageValue)
        {
            _textDamageAndHeal.font = _fontAsset;
            _textDamageAndHeal.color = _colorTextDamage;
            _textDamageAndHeal.outlineColor = _colorOutlineDamage;
            _textDamageAndHeal.text = damageValue.ToString();
        }

        private void ShowTextHeal(float healValue)
        {
            _textDamageAndHeal.font = _fontAsset;
            _textDamageAndHeal.color = _colorTextHeal;
            _textDamageAndHeal.outlineColor = _colorOutlineHeal;
            _textDamageAndHeal.text = healValue.ToString();
        }

        **************************************************************/
    }
}
