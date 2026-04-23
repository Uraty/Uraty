using System;
using UnityEngine;
using TriInspector;
using Uraty.Shared.Battle;

namespace Uraty.Feature.Player
{
    [System.Serializable]
    public struct LegacyPenetrationSettings
    {
        [SerializeField] private bool _canPenetrate;
        [SerializeField] private int _maxHitCount;
        [SerializeField] private float _continueDistanceMeters;

        public bool CanPenetrate => _canPenetrate;
        public int MaxHitCount => _maxHitCount;
        public float ContinueDistanceMeters => _continueDistanceMeters;
    }

    [CreateAssetMenu(menuName = "Game/Player/Role Definition", fileName = "RoleDefinition")]
    public class RoleDefinition : ScriptableObject
    {
        [SerializeField] private int _serializedDataVersion = 2;

        [Header("基本")]
        [SerializeField, LabelText("役職")] private RoleType _roleType;
        [SerializeField, LabelText("最大体力")] private int _maxHp = 100;
        [SerializeField, LabelText("移動速度")] private float _moveSpeed = 1f;
        [SerializeField, LabelText("最大弾薬数")] private float _maxAmmo = 3f;
        [SerializeField, LabelText("リロード間隔"), Unit("ｓ")] private float _reloadIntervalSeconds = 1f;
        [SerializeField, LabelText("攻撃時の非リロード時間"), Unit("ｓ")] private float _nonReloadSecondsAfterAttack = 0f;

        [Title("Attack")]
        [InlineProperty, HideLabel]
        [SerializeField] private AttackDefinition _attack = new AttackDefinition();

        [Title("Special")]
        [InlineProperty, HideLabel]
        [SerializeField] private AttackDefinition _special = new AttackDefinition();

        [Header("新フィールド")]
        [SerializeField] private bool _canPenetrate;
        [SerializeField] private int _maxHitCount = 1;
        [SerializeField] private float _continueDistanceMeters;

        [Header("旧データ移行用")]
        [SerializeField, HideInInspector]
        private LegacyPenetrationSettings _legacyPenetrationSettings;

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

            _attack.Validate();
            _special.Validate();
        }

        /// <summary>
        /// 指定された弾種が回復弾かどうかを返す。
        /// </summary>
        public bool IsRecovery(PlayerBulletAttackKind attackKind)
        {
            return attackKind == PlayerBulletAttackKind.Attack
                ? _attack.IsRecovery
                : _special.IsRecovery;
        }

        /// <summary>
        /// 指定された弾種が草を破壊できるかどうかを返す。
        /// </summary>
        public bool CanBreakBush(PlayerBulletAttackKind attackKind)
        {
            return attackKind == PlayerBulletAttackKind.Attack
                ? _attack.CanBreakBush
                : _special.CanBreakBush;
        }

        /// <summary>
        /// 指定された弾種が壁を破壊できるかどうかを返す。
        /// </summary>
        public bool CanBreakWalls(PlayerBulletAttackKind attackKind)
        {
            return attackKind == PlayerBulletAttackKind.Attack
                ? _attack.CanBreakWalls
                : _special.CanBreakWalls;
        }
        public bool CanMultiHit(PlayerBulletAttackKind attackKind)
        {
            return GetAttackDefinition(attackKind).CanMultiHit;
        }

        public BulletPenetrationSettings GetPenetrationSettings(PlayerBulletAttackKind attackKind)
        {
            return GetAttackDefinition(attackKind).PenetrationSettings;
        }

        private AttackDefinition GetAttackDefinition(PlayerBulletAttackKind attackKind)
        {
            return attackKind == PlayerBulletAttackKind.Attack
                ? _attack
                : _special;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (_serializedDataVersion >= 2)
            {
                return;
            }

            if (_legacyPenetrationSettings.CanPenetrate)
            {
                _canPenetrate = true;
            }

            if (_legacyPenetrationSettings.MaxHitCount > 0)
            {
                _maxHitCount = _legacyPenetrationSettings.MaxHitCount;
            }

            if (_legacyPenetrationSettings.ContinueDistanceMeters > 0.0f)
            {
                _continueDistanceMeters =
                    _legacyPenetrationSettings.ContinueDistanceMeters;
            }

            _serializedDataVersion = 2;
        }
    }

    [Serializable]
    public class AttackDefinition
    {
        [Header("共通")]
        [SerializeField, LabelText("回復")] private bool _isRecovery = false;
        [SerializeField, LabelText("壁破壊")] private bool _canBreakWalls = false;
        [SerializeField, LabelText("草破壊")] private bool _canBreakGrass = false;
        [SerializeField, LabelText("多段ヒット")] private bool _canMultiHit = false;
        [SerializeField, LabelText("貫通")] private BulletPenetrationSettings _penetrationSettings = new BulletPenetrationSettings();
        [SerializeField, LabelText("攻撃インターバル最長"), Unit("ｓ")] private float _maxAttackIntervalSeconds = 1.0f;
        [SerializeField, LabelText("攻撃インターバル最短"), Unit("ｓ")] private float _minAttackIntervalSeconds = 0.5f;
        [SerializeField, Range(0f, 100f), LabelText("必殺チャージ率"), Unit("％")] private float _specialChargePercent = 0f;
        [SerializeField, EnumToggleButtons, LabelText("攻撃タイプ")] private AimType _type = AimType.Line;

        [ShowIf(nameof(IsLine))]
        [InlineProperty, HideLabel]
        [Indent]
        [SerializeField] private LineAttackDefinition _line = new LineAttackDefinition();

        [ShowIf(nameof(IsFan))]
        [InlineProperty, HideLabel]
        [Indent]
        [SerializeField] private FanAttackDefinition _fan = new FanAttackDefinition();

        [ShowIf(nameof(IsThrow))]
        [InlineProperty, HideLabel]
        [Indent]
        [SerializeField] private ThrowAttackDefinition _throw = new ThrowAttackDefinition();

        public AimType Type => _type;
        public bool IsRecovery => _isRecovery;
        public bool CanBreakWalls => _canBreakWalls;
        public bool CanBreakBush => _canBreakGrass;
        public bool CanMultiHit => _canMultiHit;
        public BulletPenetrationSettings PenetrationSettings => _penetrationSettings;
        public float MaxAttackIntervalSeconds => _maxAttackIntervalSeconds;
        public float MinAttackIntervalSeconds => _minAttackIntervalSeconds;
        public float SpecialChargePercent => _specialChargePercent;
        public LineAttackDefinition Line => _line;
        public FanAttackDefinition Fan => _fan;
        public ThrowAttackDefinition Throw => _throw;

        private bool IsLine => _type == AimType.Line;
        private bool IsFan => _type == AimType.Fan;
        private bool IsThrow => _type == AimType.Throw;

        public void Validate()
        {
            _maxAttackIntervalSeconds = Mathf.Max(0f, _maxAttackIntervalSeconds);
            _minAttackIntervalSeconds = Mathf.Max(0f, _minAttackIntervalSeconds);
            _specialChargePercent = Mathf.Clamp(_specialChargePercent, 0f, 100f);

            _penetrationSettings ??= new BulletPenetrationSettings();
            _line ??= new LineAttackDefinition();
            _fan ??= new FanAttackDefinition();
            _throw ??= new ThrowAttackDefinition();

            _penetrationSettings.Validate();
            _line.Validate();
            _fan.Validate();
            _throw.Validate();
        }
    }

    [Serializable]
    public class LineAttackDefinition
    {
        [Title("直線")]
        [SerializeField, LabelText("弾速"), Unit("マス／ｓ")] private float _speedPerSecond = 1f;

        [Header("Aim")]
        [SerializeField, LabelText("エイム線の合計数")] private int _aimLineCount = 1;
        [SerializeField, LabelText("エイム線設定")]
        private LineAimLineDefinition[] _aimLines =
        {
        new LineAimLineDefinition()
    };

        [Header("Bullet")]
        [SerializeField, LabelText("出現する弾の合計数")] private int _bulletCount = 1;
        [SerializeField, LabelText("弾設定")]
        private LineBulletDefinition[] _bullets =
        {
        new LineBulletDefinition()
    };

        public float SpeedPerSecond => _speedPerSecond;
        public int AimLineCount => _aimLineCount;
        public LineAimLineDefinition[] AimLines => _aimLines;
        public int BulletCount => _bulletCount;
        public LineBulletDefinition[] Bullets => _bullets;

        public void Validate()
        {
            _speedPerSecond = Mathf.Max(0f, _speedPerSecond);

            _aimLineCount = Mathf.Max(1, _aimLineCount);
            DefinitionArrayUtility.EnsureSize(ref _aimLines, _aimLineCount);
            for (int i = 0; i < _aimLines.Length; i++)
            {
                _aimLines[i].Validate();
            }

            _bulletCount = Mathf.Max(1, _bulletCount);
            DefinitionArrayUtility.EnsureSize(ref _bullets, _bulletCount);
            for (int i = 0; i < _bullets.Length; i++)
            {
                _bullets[i].Validate();
            }
        }
    }

    [Serializable]
    public class FanAttackDefinition
    {
        [Title("扇")]
        [SerializeField, LabelText("弾速"), Unit("マス／ｓ")] private float _speedPerSecond = 1f;

        [Header("Aim")]
        [SerializeField, LabelText("エイム線の合計数")] private int _aimLineCount = 1;
        [SerializeField, LabelText("エイム線設定")]
        private FanAimLineDefinition[] _aimLines =
        {
        new FanAimLineDefinition()
    };

        [Header("Bullet")]
        [SerializeField, LabelText("出現する弾の合計数")] private int _bulletCount = 1;
        [SerializeField, LabelText("弾設定")]
        private FanBulletDefinition[] _bullets =
        {
        new FanBulletDefinition()
    };

        public float SpeedPerSecond => _speedPerSecond;
        public int AimLineCount => _aimLineCount;
        public FanAimLineDefinition[] AimLines => _aimLines;
        public int BulletCount => _bulletCount;
        public FanBulletDefinition[] Bullets => _bullets;

        public float RangeMeters
        {
            get
            {
                if (_aimLines == null || _aimLines.Length == 0)
                {
                    return 0f;
                }

                float maxEffectiveRange = 0f;
                for (int i = 0; i < _aimLines.Length; i++)
                {
                    FanAimLineDefinition aimLineDefinition = _aimLines[i];
                    if (aimLineDefinition == null)
                    {
                        continue;
                    }

                    maxEffectiveRange = Mathf.Max(maxEffectiveRange, aimLineDefinition.EffectiveRange);
                }

                return maxEffectiveRange;
            }
        }

        public void Validate()
        {
            _speedPerSecond = Mathf.Max(0f, _speedPerSecond);

            _aimLineCount = Mathf.Max(1, _aimLineCount);
            DefinitionArrayUtility.EnsureSize(ref _aimLines, _aimLineCount);
            for (int i = 0; i < _aimLines.Length; i++)
            {
                _aimLines[i].Validate();
            }

            _bulletCount = Mathf.Max(1, _bulletCount);
            DefinitionArrayUtility.EnsureSize(ref _bullets, _bulletCount);
            for (int i = 0; i < _bullets.Length; i++)
            {
                _bullets[i].Validate();
            }
        }
    }

    [Serializable]
    public class ThrowAttackDefinition
    {
        [Title("投擲")]
        [SerializeField, LabelText("貫通")]
        private BulletPenetrationSettings _bulletPenetrationSettings = new BulletPenetrationSettings();

        [Header("射程")]
        [SerializeField, LabelText("可変射程")] private bool _isVariableRange = false;

        [Header("着弾時間")]
        [SerializeField, LabelText("可変着弾時間")] private bool _isVariableLandingTime = false;

        [ShowIf(nameof(IsFixedLandingTime))]
        [SerializeField, LabelText("着弾時間"), Unit("ｓ")] private float _fixedLandingTimeSeconds = 1f;

        [ShowIf(nameof(IsVariableLandingTime))]
        [SerializeField, LabelText("最大着弾時間"), Unit("ｓ")] private float _maxLandingTimeSeconds = 1f;

        [ShowIf(nameof(IsVariableLandingTime))]
        [SerializeField, LabelText("最短着弾時間"), Unit("ｓ")] private float _minLandingTimeSeconds = 0.2f;


        [Header("Aim")]
        [SerializeField, LabelText("エイム線の合計数")] private int _aimLineCount = 1;
        [SerializeField, LabelText("エイム線設定")]
        private ThrowAimLineDefinition[] _aimLines =
        {
        new ThrowAimLineDefinition()
    };

        [Header("Bullet")]
        [SerializeField, LabelText("出現する弾の合計数")] private int _bulletCount = 1;
        [SerializeField, LabelText("弾設定")]
        private ThrowBulletDefinition[] _bullets =
        {
        new ThrowBulletDefinition()
    };

        public BulletPenetrationSettings PenetrationSettings => _bulletPenetrationSettings;
        public bool IsVariableRange => _isVariableRange;
        public bool IsVariableLandingTime => _isVariableLandingTime;
        public float FixedLandingTimeSeconds => _fixedLandingTimeSeconds;
        public float MaxLandingTimeSeconds => _maxLandingTimeSeconds;
        public float MinLandingTimeSeconds => _minLandingTimeSeconds;
        public int AimLineCount => _aimLineCount;
        public ThrowAimLineDefinition[] AimLines => _aimLines;
        public int BulletCount => _bulletCount;
        public ThrowBulletDefinition[] Bullets => _bullets;

        private bool IsFixedLandingTime => !_isVariableLandingTime;

        public void Validate()
        {
            _bulletPenetrationSettings ??= new BulletPenetrationSettings();
            _bulletPenetrationSettings.Validate();

            _fixedLandingTimeSeconds = Mathf.Max(0f, _fixedLandingTimeSeconds);
            _maxLandingTimeSeconds = Mathf.Max(0f, _maxLandingTimeSeconds);
            _minLandingTimeSeconds = Mathf.Max(0f, _minLandingTimeSeconds);

            _aimLineCount = Mathf.Max(1, _aimLineCount);
            DefinitionArrayUtility.EnsureSize(ref _aimLines, _aimLineCount);
            for (int i = 0; i < _aimLines.Length; i++)
            {
                _aimLines[i].Validate();
            }

            _bulletCount = Mathf.Max(1, _bulletCount);
            DefinitionArrayUtility.EnsureSize(ref _bullets, _bulletCount);
            for (int i = 0; i < _bullets.Length; i++)
            {
                _bullets[i].Validate();
            }
        }
    }

    [Serializable]
    public class LineAimLineDefinition
    {
        [SerializeField, Range(-360f, 360f), LabelText("角度オフセット"), Unit("°")]
        private float _offsetAngleFromAimLine = 0f;

        [SerializeField, LabelText("横方向オフセット"), Unit("マス")]
        private float _offsetDistanceFromAimLine = 0f;

        [SerializeField, LabelText("射程"), Unit("マス")]
        private float _range = 1f;

        [SerializeField, LabelText("有効射程"), Unit("マス")]
        private float _effectiveRange = 1f;

        [SerializeField, LabelText("横幅"), Unit("マス")]
        private float _width = 1f;

        public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
        public float OffsetDistanceFromAimLine => _offsetDistanceFromAimLine;
        public float Range => _range;
        public float EffectiveRange => _effectiveRange;
        public float Width => _width;

        public void Validate()
        {
            _range = Mathf.Max(0f, _range);
            _effectiveRange = Mathf.Max(0f, _effectiveRange);
            _width = Mathf.Max(0f, _width);
        }
    }

    [Serializable]
    public class FanAimLineDefinition
    {
        [SerializeField, Range(-360f, 360f), LabelText("角度オフセット"), Unit("°")]
        private float _offsetAngleFromAimLine = 0f;

        [SerializeField, LabelText("射程"), Unit("マス")]
        private float _range = 1f;

        [SerializeField, LabelText("有効射程"), Unit("マス")]
        private float _effectiveRange = 1f;

        [SerializeField, Range(0f, 360f), LabelText("角度"), Unit("°")]
        private float _angle = 10f;

        public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
        public float Range => _range;
        public float EffectiveRange => _effectiveRange;
        public float Angle => _angle;

        public void Validate()
        {
            _range = Mathf.Max(0f, _range);
            _effectiveRange = Mathf.Max(0f, _effectiveRange);
            _angle = Mathf.Clamp(_angle, 0f, 360f);
        }
    }

    [Serializable]
    public class ThrowAimLineDefinition
    {
        [SerializeField, Range(-360f, 360f), LabelText("角度オフセット"), Unit("°")]
        private float _offsetAngleFromAimLine = 0f;

        [SerializeField, LabelText("距離オフセット"), Unit("マス")]
        private float _offsetDistanceFromAimLine = 0f;

        [SerializeField, LabelText("最高射程"), Unit("マス")]
        private float _maxRange = 1f;

        [SerializeField, LabelText("最低射程"), Unit("マス")]
        private float _minRange = 1f;

        [SerializeField, LabelText("有効射程"), Unit("マス")]
        private float _effectiveRange = 1f;

        [SerializeField, LabelText("円半径"), Unit("マス")]
        private float _circleRadius = 1f;

        [SerializeField, LabelText("放物線横幅"), Unit("マス")]
        private float _parabolaWidth = 1f;

        [SerializeField, LabelText("固定時の放物線高度"), Unit("マス")]
        private float _fixedParabolaHeight = 1f;

        [SerializeField, LabelText("可変時の最高高度"), Unit("マス")]
        private float _maxParabolaHeight = 1f;

        [SerializeField, LabelText("可変時の最低高度"), Unit("マス")]
        private float _minParabolaHeight = 0.5f;

        public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
        public float OffsetDistanceFromAimLine => _offsetDistanceFromAimLine;
        public float MaxRange => _maxRange;
        public float MinRange => _minRange;
        public float EffectiveRange => _effectiveRange;
        public float CircleRadius => _circleRadius;
        public float ParabolaWidth => _parabolaWidth;
        public float FixedParabolaHeight => _fixedParabolaHeight;
        public float MaxParabolaHeight => _maxParabolaHeight;
        public float MinParabolaHeight => _minParabolaHeight;

        public void Validate()
        {
            _maxRange = Mathf.Max(0f, _maxRange);
            _minRange = Mathf.Max(_minRange, 1.0f);
            _effectiveRange = Mathf.Max(0f, _effectiveRange);
            _circleRadius = Mathf.Max(0f, _circleRadius);
            _parabolaWidth = Mathf.Max(0f, _parabolaWidth);
            _fixedParabolaHeight = Mathf.Max(0f, _fixedParabolaHeight);
            _maxParabolaHeight = Mathf.Max(0f, _maxParabolaHeight);
            _minParabolaHeight = Mathf.Max(0f, _minParabolaHeight);
        }
    }

    [Serializable]
    public class LineBulletDefinition
    {
        [SerializeField, LabelText("弾のプレハブ")] private GameObject _bulletPrefab = null;
        [SerializeField, LabelText("出てくるまでの秒数"), Unit("ｓ")] private float _spawnDelaySeconds = 0f;
        [SerializeField, LabelText("エイム線からの弾のずれ幅"), Unit("マス")] private float _offsetFromAimLine = 0f;
        [SerializeField, LabelText("プレイヤー中心からの生成オフセット")] private Vector3 _spawnOffsetFromPlayerCenter = Vector3.zero;
        [SerializeField, LabelText("弾の横幅"), Unit("マス")] private float _bulletWidth = 1f;
        [SerializeField, LabelText("弾の縦幅"), Unit("マス")] private float _bulletHeight = 1f;

        public GameObject BulletPrefab => _bulletPrefab;
        public float SpawnDelaySeconds => _spawnDelaySeconds;
        public float OffsetFromAimLine => _offsetFromAimLine;
        public Vector3 SpawnOffsetFromPlayerCenter => _spawnOffsetFromPlayerCenter;
        public float BulletWidth => _bulletWidth;
        public float BulletHeight => _bulletHeight;

        public void Validate()
        {
            _spawnDelaySeconds = Mathf.Max(0f, _spawnDelaySeconds);
            _bulletWidth = Mathf.Max(0f, _bulletWidth);
            _bulletHeight = Mathf.Max(0f, _bulletHeight);
        }
    }

    [Serializable]
    public class FanBulletDefinition
    {
        [SerializeField, LabelText("弾のプレハブ")] private GameObject _bulletPrefab = null;
        [SerializeField, LabelText("出てくるまでの秒数"), Unit("ｓ")] private float _spawnDelaySeconds = 0f;
        [SerializeField, Range(-360f, 360f), LabelText("エイム線からの弾のずれ度"), Unit("°")] private float _offsetAngleFromAimLine = 0f;
        [SerializeField, LabelText("プレイヤー中心からの生成オフセット")] private Vector3 _spawnOffsetFromPlayerCenter = Vector3.zero;
        [SerializeField, LabelText("弾の高さ"), Unit("マス")] private float _height = 1f;
        [SerializeField, Range(0f, 360f), LabelText("弾の角度"), Unit("°")] private float _angle = 10f;

        public GameObject BulletPrefab => _bulletPrefab;
        public float SpawnDelaySeconds => _spawnDelaySeconds;
        public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
        public float OffsetAngleFromCenter => _offsetAngleFromAimLine;
        public Vector3 SpawnOffsetFromPlayerCenter => _spawnOffsetFromPlayerCenter;
        public float Height => _height;
        public float Angle => _angle;

        public void Validate()
        {
            _spawnDelaySeconds = Mathf.Max(0f, _spawnDelaySeconds);
            _height = Mathf.Max(0f, _height);
            _angle = Mathf.Clamp(_angle, 0f, 360f);
        }
    }

    [Serializable]
    public class ThrowBulletDefinition
    {
        [SerializeField, LabelText("弾のプレハブ")] private GameObject _bulletPrefab = null;
        [SerializeField, LabelText("出てくるまでの秒数"), Unit("ｓ")] private float _spawnDelaySeconds = 0f;
        [SerializeField, LabelText("弾の半径"), Unit("マス")] private float _radius = 1f;
        [SerializeField, LabelText("エイム線（円）からの弾のずれ幅"), Unit("マス")] private float _offsetDistanceFromAimLine = 0f;
        [SerializeField, Range(-360f, 360f), LabelText("エイム線（円）からの弾のずれ度"), Unit("°")] private float _offsetAngleFromAimLine = 0f;
        [SerializeField, LabelText("プレイヤー中心からの生成オフセット")] private Vector3 _spawnOffsetFromPlayerCenter = Vector3.zero;

        public GameObject BulletPrefab => _bulletPrefab;
        public float SpawnDelaySeconds => _spawnDelaySeconds;
        public float Radius => _radius;
        public float OffsetDistanceFromAimLine => _offsetDistanceFromAimLine;
        public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
        public Vector3 SpawnOffsetFromPlayerCenter => _spawnOffsetFromPlayerCenter;

        public void Validate()
        {
            _spawnDelaySeconds = Mathf.Max(0f, _spawnDelaySeconds);
            _radius = Mathf.Max(0f, _radius);
        }
    }

    internal static class DefinitionArrayUtility
    {
        public static void EnsureSize<T>(ref T[] array, int size) where T : class, new()
        {
            size = Mathf.Max(0, size);

            if (array == null)
            {
                array = new T[size];
            }
            else if (array.Length != size)
            {
                Array.Resize(ref array, size);
            }

            for (int i = 0; i < array.Length; i++)
            {
                array[i] ??= new T();
            }
        }
    }
}
