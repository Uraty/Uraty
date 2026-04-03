using System;
using UnityEngine;
using TriInspector;

namespace Uraty.Feature.Player
{
    [CreateAssetMenu(menuName = "Game/Player/Role Definition", fileName = "RoleDefinition")]
    public class RoleDefinition : ScriptableObject
    {
        [Header("基本")]
        [SerializeField] private RoleType _roleType;

        [Header("HP")]
        [SerializeField] private int _maxHp = 100;

        [Header("移動")]
        [SerializeField] private float _moveSpeed = 1f;

        [Header("弾")]
        [SerializeField] private float _maxAmmo = 3f;
        [SerializeField] private float _reloadIntervalSeconds = 1f;
        [SerializeField] private float _nonReloadSecondsAfterAttack = 0f;

        [Title("Attack")]
        [InlineProperty, HideLabel]
        [SerializeField] private AttackDefinition _attack = new();

        [Title("Special")]
        [InlineProperty, HideLabel]
        [SerializeField] private AttackDefinition _special = new();

        public RoleType RoleType => _roleType;
        public int MaxHp => _maxHp;
        public float MoveSpeed => _moveSpeed;
        public float ReloadIntervalSeconds => _reloadIntervalSeconds;
        public float MaxAmmo => _maxAmmo;
        public float NonReloadSecondsAfterAttack => _nonReloadSecondsAfterAttack;
        public AttackDefinition Attack => _attack;
        public AttackDefinition Special => _special;

        private void OnValidate()
        {
            _maxHp = Mathf.Max(1, _maxHp);
            _moveSpeed = Mathf.Max(0f, _moveSpeed);
            _maxAmmo = Mathf.Max(0f, _maxAmmo);
            _reloadIntervalSeconds = Mathf.Max(0f, _reloadIntervalSeconds);
            _nonReloadSecondsAfterAttack = Mathf.Max(0f, _nonReloadSecondsAfterAttack);

            _attack ??= new AttackDefinition();
            _special ??= new AttackDefinition();

            _attack.Sanitize();
            _special.Sanitize();
        }
    }

    [Serializable]
    public class AttackDefinition
    {
        [Header("共通")]
        [SerializeField] private bool _isRecovery = false;
        [SerializeField] private bool _canBreakWalls = false;
        [SerializeField] private bool _canBreakGrass = false;
        [SerializeField] private float _attackIntervalSeconds = 0.5f;
        [SerializeField, Range(0f, 100f)] private float _specialChargePercent = 0f;
        [SerializeField, EnumToggleButtons] private AimType _type = AimType.Line;

        [ShowIf(nameof(IsLine))]
        [InlineProperty, HideLabel]
        [Indent]
        [SerializeField] private LineAttackDefinition _line = new();

        [ShowIf(nameof(IsFan))]
        [InlineProperty, HideLabel]
        [Indent]
        [SerializeField] private FanAttackDefinition _fan = new();

        [ShowIf(nameof(IsThrow))]
        [InlineProperty, HideLabel]
        [Indent]
        [SerializeField] private ThrowAttackDefinition _throw = new();

        public AimType Type => _type;
        public bool IsRecovery => _isRecovery;
        public bool CanBreakWalls => _canBreakWalls;
        public bool CanBreakGrass => _canBreakGrass;
        public float AttackIntervalSeconds => _attackIntervalSeconds;
        public float SpecialChargePercent => _specialChargePercent;
        public LineAttackDefinition Line => _line;
        public FanAttackDefinition Fan => _fan;
        public ThrowAttackDefinition Throw => _throw;

        private bool IsLine => _type == AimType.Line;
        private bool IsFan => _type == AimType.Fan;
        private bool IsThrow => _type == AimType.Throw;

        public void Sanitize()
        {
            _attackIntervalSeconds = Mathf.Max(0f, _attackIntervalSeconds);
            _specialChargePercent = Mathf.Clamp(_specialChargePercent, 0f, 100f);

            _line ??= new LineAttackDefinition();
            _fan ??= new FanAttackDefinition();
            _throw ??= new ThrowAttackDefinition();

            _line.Sanitize();
            _fan.Sanitize();
            _throw.Sanitize();
        }
    }

    [Serializable]
    public class LineAttackDefinition
    {
        [Title("直線")]
        [SerializeField] private bool _canPierce = false;
        [SerializeField] private float _range = 1f;
        [SerializeField] private float _effectiveRange = 1f;
        [SerializeField] private float _speedPerSecond = 1f;
        [SerializeField] private float _aimWidth = 1f;
        [SerializeField, Min(1)] private int _bulletCount = 1;

        [ShowIf(nameof(IsSingleShot))]
        [Indent]
        [InlineProperty, HideLabel]
        [SerializeField] private SingleLineBulletDefinition _singleBullet = new();

        [ShowIf(nameof(IsMultiShot))]
        [Indent]
        [InlineProperty, HideLabel]
        [SerializeField] private MultiLineBulletDefinition[] _multiBullets = Array.Empty<MultiLineBulletDefinition>();

        public bool CanPierce => _canPierce;
        public float Range => _range;
        public float EffectiveRange => _effectiveRange;
        public float SpeedPerSecond => _speedPerSecond;
        public int BulletCount => _bulletCount;
        public float AimWidth => _aimWidth;
        public SingleLineBulletDefinition SingleBullet => _singleBullet;
        public MultiLineBulletDefinition[] MultiBullets => _multiBullets;

        public bool IsSingleShot => _bulletCount == 1;
        public bool IsMultiShot => _bulletCount > 1;

        public void Sanitize()
        {
            _range = Mathf.Max(0f, _range);
            _effectiveRange = Mathf.Max(0f, _effectiveRange);
            _speedPerSecond = Mathf.Max(0f, _speedPerSecond);
            _aimWidth = Mathf.Max(0f, _aimWidth);
            _bulletCount = Mathf.Max(1, _bulletCount);

            _singleBullet ??= new SingleLineBulletDefinition();
            _singleBullet.Sanitize();

            if (IsMultiShot)
            {
                if (_multiBullets == null || _multiBullets.Length != _bulletCount)
                {
                    MultiLineBulletDefinition[] newBullets = new MultiLineBulletDefinition[_bulletCount];

                    for (int i = 0; i < _bulletCount; i++)
                    {
                        if (_multiBullets != null && i < _multiBullets.Length && _multiBullets[i] != null)
                        {
                            newBullets[i] = _multiBullets[i];
                        }
                        else
                        {
                            newBullets[i] = new MultiLineBulletDefinition();
                        }
                    }

                    _multiBullets = newBullets;
                }

                for (int i = 0; i < _multiBullets.Length; i++)
                {
                    _multiBullets[i] ??= new MultiLineBulletDefinition();
                    _multiBullets[i].Sanitize();
                }
            }
            else
            {
                _multiBullets ??= Array.Empty<MultiLineBulletDefinition>();
            }
        }
    }

    [Serializable]
    public class FanAttackDefinition
    {
        [Title("扇")]
        [SerializeField] private bool _canMultiHit = false;
        [SerializeField] private bool _canPierce = false;
        [SerializeField] private float _range = 1f;
        [SerializeField] private float _effectiveRange = 1f;
        [SerializeField] private float _speedPerSecond = 1f;
        [SerializeField, Range(0f, 360f)] private float _aimAngle = 60f;
        [SerializeField, Min(1)] private int _bulletCount = 1;

        [ShowIf(nameof(IsSingleShot))]
        [Indent]
        [InlineProperty, HideLabel]
        [SerializeField] private SingleFanBulletDefinition _singleBullet = new();

        [ShowIf(nameof(IsMultiShot))]
        [Indent]
        [SerializeField] private MultiFanBulletDefinition[] _multiBullets = Array.Empty<MultiFanBulletDefinition>();

        public bool CanMultiHit => _canMultiHit;
        public bool CanPierce => _canPierce;
        public float Range => _range;
        public float EffectiveRange => _effectiveRange;
        public float SpeedPerSecond => _speedPerSecond;
        public int BulletCount => _bulletCount;
        public float AimAngle => _aimAngle;
        public SingleFanBulletDefinition SingleBullet => _singleBullet;
        public MultiFanBulletDefinition[] MultiBullets => _multiBullets;

        public bool IsSingleShot => _bulletCount == 1;
        public bool IsMultiShot => _bulletCount > 1;

        public void Sanitize()
        {
            _range = Mathf.Max(0f, _range);
            _effectiveRange = Mathf.Max(0f, _effectiveRange);
            _speedPerSecond = Mathf.Max(0f, _speedPerSecond);
            _aimAngle = Mathf.Clamp(_aimAngle, 0f, 360f);
            _bulletCount = Mathf.Max(1, _bulletCount);

            _singleBullet ??= new SingleFanBulletDefinition();
            _singleBullet.Sanitize();

            if (IsMultiShot)
            {
                if (_multiBullets == null || _multiBullets.Length != _bulletCount)
                {
                    MultiFanBulletDefinition[] newBullets = new MultiFanBulletDefinition[_bulletCount];

                    for (int i = 0; i < _bulletCount; i++)
                    {
                        if (_multiBullets != null && i < _multiBullets.Length && _multiBullets[i] != null)
                        {
                            newBullets[i] = _multiBullets[i];
                        }
                        else
                        {
                            newBullets[i] = new MultiFanBulletDefinition();
                        }
                    }

                    _multiBullets = newBullets;
                }

                for (int i = 0; i < _multiBullets.Length; i++)
                {
                    _multiBullets[i] ??= new MultiFanBulletDefinition();
                    _multiBullets[i].Sanitize();
                }
            }
            else
            {
                _multiBullets ??= Array.Empty<MultiFanBulletDefinition>();
            }
        }
    }

    [Serializable]
    public class ThrowAttackDefinition
    {
        [Title("投擲")]
        [Header("射程")]
        [SerializeField] private bool _isVariableRange = false;

        [ShowIf(nameof(IsFixedRange))]
        [SerializeField] private float _range = 1f;

        [ShowIf(nameof(IsFixedRange))]
        [SerializeField] private float _effectiveRange = 1f;

        [ShowIf(nameof(IsVariableRange))]
        [SerializeField] private float _maxRange = 1f;

        [ShowIf(nameof(IsVariableRange))]
        [SerializeField] private float _maxEffectiveRange = 1f;

        [Header("着弾時間")]
        [SerializeField] private bool _isVariableLandingTime = false;

        [ShowIf(nameof(IsFixedLandingTime))]
        [SerializeField] private float _fixedLandingTimeSeconds = 1f;

        [ShowIf(nameof(IsVariableLandingTime))]
        [SerializeField] private float _maxLandingTimeSeconds = 1f;

        [ShowIf(nameof(IsVariableLandingTime))]
        [SerializeField] private float _minLandingTimeSeconds = 0.2f;

        [Header("弾")]
        [SerializeField] private float _aimRadius = 1f;
        [SerializeField, Min(1)] private int _bulletCount = 1;

        [ShowIf(nameof(IsSingleShot))]
        [InlineProperty, HideLabel]
        [SerializeField] private SingleThrowBulletDefinition _singleBullet = new();

        [ShowIf(nameof(IsMultiShot))]
        [SerializeField] private MultiThrowBulletDefinition[] _multiBullets = Array.Empty<MultiThrowBulletDefinition>();

        public bool IsVariableRange => _isVariableRange;
        public float Range => _range;
        public float EffectiveRange => _effectiveRange;
        public float MaxRange => _maxRange;
        public float MaxEffectiveRange => _maxEffectiveRange;

        public bool IsVariableLandingTime => _isVariableLandingTime;
        public float FixedLandingTimeSeconds => _fixedLandingTimeSeconds;
        public float MaxLandingTimeSeconds => _maxLandingTimeSeconds;
        public float MinLandingTimeSeconds => _minLandingTimeSeconds;

        public int BulletCount => _bulletCount;
        public float AimRadius => _aimRadius;
        public SingleThrowBulletDefinition SingleBullet => _singleBullet;
        public MultiThrowBulletDefinition[] MultiBullets => _multiBullets;

        public bool IsSingleShot => _bulletCount == 1;
        public bool IsMultiShot => _bulletCount > 1;

        private bool IsFixedRange => !_isVariableRange;
        private bool IsFixedLandingTime => !_isVariableLandingTime;

        public void Sanitize()
        {
            _range = Mathf.Max(0f, _range);
            _effectiveRange = Mathf.Max(0f, _effectiveRange);
            _maxRange = Mathf.Max(0f, _maxRange);
            _maxEffectiveRange = Mathf.Max(0f, _maxEffectiveRange);

            _fixedLandingTimeSeconds = Mathf.Max(0f, _fixedLandingTimeSeconds);
            _maxLandingTimeSeconds = Mathf.Max(0f, _maxLandingTimeSeconds);
            _minLandingTimeSeconds = Mathf.Max(0f, _minLandingTimeSeconds);

            _aimRadius = Mathf.Max(0f, _aimRadius);
            _bulletCount = Mathf.Max(1, _bulletCount);

            _singleBullet ??= new SingleThrowBulletDefinition();
            _singleBullet.Sanitize();

            if (IsMultiShot)
            {
                if (_multiBullets == null || _multiBullets.Length != _bulletCount)
                {
                    MultiThrowBulletDefinition[] newBullets = new MultiThrowBulletDefinition[_bulletCount];

                    for (int i = 0; i < _bulletCount; i++)
                    {
                        if (_multiBullets != null && i < _multiBullets.Length && _multiBullets[i] != null)
                        {
                            newBullets[i] = _multiBullets[i];
                        }
                        else
                        {
                            newBullets[i] = new MultiThrowBulletDefinition();
                        }
                    }

                    _multiBullets = newBullets;
                }

                for (int i = 0; i < _multiBullets.Length; i++)
                {
                    _multiBullets[i] ??= new MultiThrowBulletDefinition();
                    _multiBullets[i].Sanitize();
                }
            }
            else
            {
                _multiBullets ??= Array.Empty<MultiThrowBulletDefinition>();
            }
        }
    }

    [Serializable]
    public class SingleLineBulletDefinition
    {
        [Title("単発")]
        [SerializeField] private float _bulletWidth = 1f;
        [SerializeField] private float _bulletHeight = 1f;

        public float BulletWidth => _bulletWidth;
        public float BulletHeight => _bulletHeight;

        public void Sanitize()
        {
            _bulletWidth = Mathf.Max(0f, _bulletWidth);
            _bulletHeight = Mathf.Max(0f, _bulletHeight);
        }
    }

    [Serializable]
    public class MultiLineBulletDefinition
    {
        [SerializeField] private float _spawnDelaySeconds = 0f;
        [SerializeField] private float _offsetFromAimLine = 0f;
        [SerializeField] private float _bulletWidth = 1f;
        [SerializeField] private float _bulletHeight = 1f;

        public float SpawnDelaySeconds => _spawnDelaySeconds;
        public float OffsetFromAimLine => _offsetFromAimLine;
        public float BulletWidth => _bulletWidth;
        public float BulletHeight => _bulletHeight;

        public void Sanitize()
        {
            _spawnDelaySeconds = Mathf.Max(0f, _spawnDelaySeconds);
            _bulletWidth = Mathf.Max(0f, _bulletWidth);
            _bulletHeight = Mathf.Max(0f, _bulletHeight);
        }
    }

    [Serializable]
    public class SingleFanBulletDefinition
    {
        [Title("単発")]
        [SerializeField] private float _height = 1f;
        [SerializeField, Range(0f, 360f)] private float _angle = 10f;

        public float Height => _height;
        public float Angle => _angle;

        public void Sanitize()
        {
            _height = Mathf.Max(0f, _height);
            _angle = Mathf.Clamp(_angle, 0f, 360f);
        }
    }

    [Serializable]
    public class MultiFanBulletDefinition
    {
        [SerializeField] private float _spawnDelaySeconds = 0f;
        [SerializeField, Range(-360f, 360f)] private float _offsetAngleFromAimLine = 0f;
        [SerializeField] private float _height = 1f;
        [SerializeField, Range(0f, 360f)] private float _angle = 10f;

        public float SpawnDelaySeconds => _spawnDelaySeconds;
        public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
        public float Height => _height;
        public float Angle => _angle;

        public void Sanitize()
        {
            _spawnDelaySeconds = Mathf.Max(0f, _spawnDelaySeconds);
            _offsetAngleFromAimLine = Mathf.Clamp(_offsetAngleFromAimLine, -360f, 360f);
            _height = Mathf.Max(0f, _height);
            _angle = Mathf.Clamp(_angle, 0f, 360f);
        }
    }

    [Serializable]
    public class SingleThrowBulletDefinition
    {
        [Title("単発")]
        [SerializeField] private float _radius = 1f;

        public float Radius => _radius;

        public void Sanitize()
        {
            _radius = Mathf.Max(0f, _radius);
        }
    }

    [Serializable]
    public class MultiThrowBulletDefinition
    {
        [SerializeField] private float _spawnDelaySeconds = 0f;
        [SerializeField] private float _radius = 1f;
        [SerializeField] private float _offsetDistanceFromAimLine = 0f;
        [SerializeField, Range(-360f, 360f)] private float _offsetAngleFromAimLine = 0f;

        public float SpawnDelaySeconds => _spawnDelaySeconds;
        public float Radius => _radius;
        public float OffsetDistanceFromAimLine => _offsetDistanceFromAimLine;
        public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;

        public void Sanitize()
        {
            _spawnDelaySeconds = Mathf.Max(0f, _spawnDelaySeconds);
            _radius = Mathf.Max(0f, _radius);
            _offsetDistanceFromAimLine = Mathf.Max(0f, _offsetDistanceFromAimLine);
            _offsetAngleFromAimLine = Mathf.Clamp(_offsetAngleFromAimLine, -360f, 360f);
        }
    }
}
