using UnityEngine;

namespace Uraty.Feature.Player
{
    [CreateAssetMenu(menuName = "Game/Player/Role Definition", fileName = "RoleDefinition")]
    public class RoleDefinition : ScriptableObject
    {
        [Header("基本")]
        [SerializeField] private RoleType _roleType;

        [Header("HP")]
        [SerializeField, Min(1f)] private float _maxHp = 100f;

        [Header("弾数")]
        [SerializeField, Min(0.01f)] private float _maxAmmo = 3f;
        [SerializeField, Min(0.01f)] private float _ammoRecoverIntervalSeconds = 1.5f;

        [Header("攻撃")]
        [SerializeField, Min(0.01f)] private float _attackIntervalSeconds = 0.5f;
        [SerializeField, Min(0f)] private float _attackDamage = 10f;
        [SerializeField, Min(0f)] private float _attackRange = 1.5f;

        [Header("スキル")]
        //[SerializeField, Min(0.01f)] private float _skillIntervalSeconds = 5f;
        //[SerializeField, Min(0f)] private float _skillDamage = 30f;
        //[SerializeField, Min(0f)] private float _skillRange = 3f;

        [Header("必殺")]
        //[SerializeField, Min(0.01f)] private float _ultimateIntervalSeconds = 10f;
        //[SerializeField, Min(0f)] private float _ultimateDamage = 50f;
        //[SerializeField, Min(0f)] private float _ultimateRange = 5f;

        [Header("移動")]
        [SerializeField, Min(0f)] private float _moveSpeed = 5f;

        public RoleType RoleType => _roleType;
        public float MaxHp => _maxHp;
        public float MaxAmmo => _maxAmmo;
        public float AmmoRecoverIntervalSeconds => _ammoRecoverIntervalSeconds;
        public float AttackIntervalSeconds => _attackIntervalSeconds;
        public float AttackDamage => _attackDamage;
        public float AttackRange => _attackRange;
        public float MoveSpeed => _moveSpeed;
    }
}
