using UnityEngine;
using TMPro;

namespace Uraty.Features.Character
{
    public sealed class HPSystem : MonoBehaviour
    {
        private const float MinBillboardDirectionSqrMagnitude = 0.0001f;

        [SerializeField] private CharacterStatus _characterStatus;

        [Header("HP Bar")]
        [SerializeField] private BarBase _barCurrentHP;
        [SerializeField] private BarBase _barDamage;
        [SerializeField] private TextMeshProUGUI _textCurrentHP;

        [Header("Billboard")]
        [SerializeField] private bool _isBillboardEnabled = true;
        [SerializeField] private bool _isYawOnlyBillboard;

        [Header("Damage Animation")]
        [SerializeField] private float _damageRatioDownSpeed = 0.002f;

        private Camera _mainCamera;

        private float _lastMaxHP;
        private float _lastCurrentHP;

        private bool _isDamage;
        private bool _isInitialized;

        public bool IsDamage => _isDamage;

        private void Awake()
        {
            CacheCharacterStatus();
            CacheMainCamera();
        }

        private void Start()
        {
            ForceRefreshHPView();
        }

        private void Update()
        {
            SyncHPView();
            UpdateDamageBar();
        }

        private void LateUpdate()
        {
            UpdateBillboard();
        }

        private void OnValidate()
        {
            if (_characterStatus == null)
            {
                _characterStatus = GetComponentInParent<CharacterStatus>();
            }
        }

        /// <summary>
        /// CharacterStatusをキャッシュする。
        /// </summary>
        private void CacheCharacterStatus()
        {
            if (_characterStatus != null)
            {
                return;
            }

            _characterStatus = GetComponentInParent<CharacterStatus>();
        }

        /// <summary>
        /// MainCameraタグが付いているカメラをキャッシュする。
        /// </summary>
        private void CacheMainCamera()
        {
            if (_mainCamera != null)
            {
                return;
            }

            _mainCamera = Camera.main;
        }

        /// <summary>
        /// HP表示をCharacterStatusの値で強制的に更新する。
        /// </summary>
        private void ForceRefreshHPView()
        {
            if (!CanUpdateHPView())
            {
                return;
            }

            float maxHP = Mathf.Max(1.0f, _characterStatus.MaxHp);
            float currentHP = Mathf.Clamp(
                _characterStatus.CurrentHp,
                0.0f,
                maxHP);

            float hpRatio = Mathf.Clamp01(currentHP / maxHP);

            _barCurrentHP.SetBarRatio(hpRatio);
            _barDamage.SetBarRatio(hpRatio);
            SetTextCurrentHP(currentHP);

            _lastMaxHP = maxHP;
            _lastCurrentHP = currentHP;

            _isDamage = false;
            _isInitialized = true;
        }

        /// <summary>
        /// CharacterStatusのHP変更を表示へ反映する。
        /// </summary>
        private void SyncHPView()
        {
            if (!CanUpdateHPView())
            {
                return;
            }

            if (!_isInitialized)
            {
                ForceRefreshHPView();
                return;
            }

            float maxHP = Mathf.Max(1.0f, _characterStatus.MaxHp);
            float currentHP = Mathf.Clamp(
                _characterStatus.CurrentHp,
                0.0f,
                maxHP);

            bool isMaxHPChanged = !Mathf.Approximately(maxHP, _lastMaxHP);
            bool isCurrentHPChanged = !Mathf.Approximately(currentHP, _lastCurrentHP);

            if (!isMaxHPChanged && !isCurrentHPChanged)
            {
                return;
            }

            float hpRatio = Mathf.Clamp01(currentHP / maxHP);

            _barCurrentHP.SetBarRatio(hpRatio);
            SetTextCurrentHP(currentHP);

            if (isMaxHPChanged)
            {
                _barDamage.SetBarRatio(hpRatio);
                _isDamage = false;
            }
            else if (currentHP < _lastCurrentHP)
            {
                _isDamage = true;
            }
            else
            {
                _barDamage.SetBarRatio(hpRatio);
                _isDamage = false;
            }

            _lastMaxHP = maxHP;
            _lastCurrentHP = currentHP;
        }

        /// <summary>
        /// HP表示に必要な参照が設定されているか確認する。
        /// </summary>
        /// <returns>HP表示を更新できる場合はtrue。</returns>
        private bool CanUpdateHPView()
        {
            return
                _characterStatus != null &&
                _barCurrentHP != null &&
                _barDamage != null &&
                _textCurrentHP != null;
        }

        /// <summary>
        /// HP表示用CanvasをMainCameraに対してビルボードさせる。
        /// </summary>
        private void UpdateBillboard()
        {
            if (!_isBillboardEnabled)
            {
                return;
            }

            if (_mainCamera == null)
            {
                CacheMainCamera();

                if (_mainCamera == null)
                {
                    return;
                }
            }

            Transform cameraTransform = _mainCamera.transform;

            if (_isYawOnlyBillboard)
            {
                Vector3 forward = cameraTransform.forward;
                forward.y = 0.0f;

                if (forward.sqrMagnitude <= MinBillboardDirectionSqrMagnitude)
                {
                    return;
                }

                transform.rotation = Quaternion.LookRotation(
                    forward.normalized,
                    Vector3.up);

                return;
            }

            transform.rotation = Quaternion.LookRotation(
                cameraTransform.forward,
                cameraTransform.up);
        }

        /// <summary>
        /// ダメージ用の遅延バーを現在HPバーへ近づける。
        /// </summary>
        private void UpdateDamageBar()
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
                _damageRatioDownSpeed);

            _barDamage.SetBarRatio(damageRatio);

            if (Mathf.Approximately(damageRatio, currentRatio))
            {
                _isDamage = false;
            }
        }

        /// <summary>
        /// 現在HPテキストを更新する。
        /// </summary>
        /// <param name="currentHP">現在HP。</param>
        private void SetTextCurrentHP(float currentHP)
        {
            _textCurrentHP.text = Mathf.CeilToInt(currentHP).ToString();
        }
    }
}
