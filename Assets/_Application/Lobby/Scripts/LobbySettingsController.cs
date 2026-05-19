using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Uraty.Feature.Setting;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビーの設定画面を管理するクラス。
    /// Sliderの表示更新と、設定値の保存要求を担当する。
    /// </summary>
    public sealed class LobbySettingsController : MonoBehaviour
    {
        [Header("Panels")]
        // ロビー通常画面。
        [SerializeField] private GameObject _mainPanel;

        // ロビー中央のキャラ表示などを含む画面。
        [SerializeField] private GameObject _modelPanel;

        // 設定画面。
        [SerializeField] private GameObject _settingPanel;

        [Header("Buttons")]
        // 設定画面を開くボタン。
        [SerializeField] private Button _openSettingButton;

        // 設定画面を閉じるボタン。
        [SerializeField] private Button _closeSettingButton;

        [Header("Sliders")]
        // マウス感度。
        [SerializeField] private Slider _mouseSensitivitySlider;

        // スティック感度。
        [SerializeField] private Slider _stickSensitivitySlider;

        // キーボード・マウス操作用デッドゾーン。
        [SerializeField] private Slider _keyMouseDeadZoneSlider;

        // スティック操作用デッドゾーン。
        [SerializeField] private Slider _stickDeadZoneSlider;

        // 効果音音量。
        [SerializeField] private Slider _seVolumeSlider;

        // BGM音量。
        [SerializeField] private Slider _bgmVolumeSlider;

        [Header("Value Texts")]
        // マウス感度の現在値表示。
        [SerializeField] private TextMeshProUGUI _mouseSensitivityValueText;

        // スティック感度の現在値表示。
        [SerializeField] private TextMeshProUGUI _stickSensitivityValueText;

        // キーボード・マウス操作用デッドゾーンの現在値表示。
        [SerializeField] private TextMeshProUGUI _keyMouseDeadZoneValueText;

        // スティック操作用デッドゾーンの現在値表示。
        [SerializeField] private TextMeshProUGUI _stickDeadZoneValueText;

        // 効果音音量の現在値表示。
        [SerializeField] private TextMeshProUGUI _seVolumeValueText;

        // BGM音量の現在値表示。
        [SerializeField] private TextMeshProUGUI _bgmVolumeValueText;

        /// <summary>
        /// 現在のSlider値を設定データとして取得する。
        /// LobbyからBattleへ設定値を渡すときにも使用できる。
        /// </summary>
        public GameSettingsData CurrentSettings
        {
            get
            {
                return new GameSettingsData(
                    _mouseSensitivitySlider.value,
                    _stickSensitivitySlider.value,
                    _keyMouseDeadZoneSlider.value,
                    _stickDeadZoneSlider.value,
                    _seVolumeSlider.value,
                    _bgmVolumeSlider.value
                );
            }
        }

        private void OnEnable()
        {
            // ボタン操作の登録。
            _openSettingButton.onClick.AddListener(HandleOpenSettingButtonClicked);
            _closeSettingButton.onClick.AddListener(HandleCloseSettingButtonClicked);

            // Slider変更時の登録。
            _mouseSensitivitySlider.onValueChanged.AddListener(HandleMouseSensitivityChanged);
            _stickSensitivitySlider.onValueChanged.AddListener(HandleStickSensitivityChanged);
            _keyMouseDeadZoneSlider.onValueChanged.AddListener(HandleKeyMouseDeadZoneChanged);
            _stickDeadZoneSlider.onValueChanged.AddListener(HandleStickDeadZoneChanged);
            _seVolumeSlider.onValueChanged.AddListener(HandleSeVolumeChanged);
            _bgmVolumeSlider.onValueChanged.AddListener(HandleBgmVolumeChanged);
        }

        private void Start()
        {
            // 保存済み設定を読み込み、UIへ反映する。
            LoadSettings();

            // 起動時は設定画面を閉じた状態にする。
            CloseSettingPanel();
        }

        private void OnDisable()
        {
            // ボタン操作の登録解除。
            _openSettingButton.onClick.RemoveListener(HandleOpenSettingButtonClicked);
            _closeSettingButton.onClick.RemoveListener(HandleCloseSettingButtonClicked);

            // Slider変更時の登録解除。
            _mouseSensitivitySlider.onValueChanged.RemoveListener(HandleMouseSensitivityChanged);
            _stickSensitivitySlider.onValueChanged.RemoveListener(HandleStickSensitivityChanged);
            _keyMouseDeadZoneSlider.onValueChanged.RemoveListener(HandleKeyMouseDeadZoneChanged);
            _stickDeadZoneSlider.onValueChanged.RemoveListener(HandleStickDeadZoneChanged);
            _seVolumeSlider.onValueChanged.RemoveListener(HandleSeVolumeChanged);
            _bgmVolumeSlider.onValueChanged.RemoveListener(HandleBgmVolumeChanged);
        }

        /// <summary>
        /// 設定画面を開くボタンが押されたときの処理。
        /// </summary>
        private void HandleOpenSettingButtonClicked()
        {
            OpenSettingPanel();
        }

        /// <summary>
        /// 設定画面を閉じるボタンが押されたときの処理。
        /// </summary>
        private void HandleCloseSettingButtonClicked()
        {
            CloseSettingPanel();
        }

        /// <summary>
        /// 設定画面を開く。
        /// </summary>
        private void OpenSettingPanel()
        {
            _mainPanel.SetActive(false);
            _modelPanel.SetActive(false);
            _settingPanel.SetActive(true);
        }

        /// <summary>
        /// 設定画面を閉じて、通常のロビー画面へ戻す。
        /// </summary>
        private void CloseSettingPanel()
        {
            _settingPanel.SetActive(false);
            _mainPanel.SetActive(true);
            _modelPanel.SetActive(true);
        }

        /// <summary>
        /// 保存済み設定を読み込み、Sliderと表示テキストに反映する。
        /// </summary>
        private void LoadSettings()
        {
            GameSettingsData settings = GameSettingsStore.Load();

            ApplySettingsToSliders(settings);
            UpdateAllTexts();
        }

        /// <summary>
        /// 読み込んだ設定値をSliderへ反映する。
        /// SetValueWithoutNotifyを使い、読み込み時に保存処理が走らないようにする。
        /// </summary>
        private void ApplySettingsToSliders(GameSettingsData settings)
        {
            _mouseSensitivitySlider.SetValueWithoutNotify(settings.MouseSensitivity);
            _stickSensitivitySlider.SetValueWithoutNotify(settings.StickSensitivity);
            _keyMouseDeadZoneSlider.SetValueWithoutNotify(settings.KeyMouseDeadZone);
            _stickDeadZoneSlider.SetValueWithoutNotify(settings.StickDeadZone);
            _seVolumeSlider.SetValueWithoutNotify(settings.SeVolume);
            _bgmVolumeSlider.SetValueWithoutNotify(settings.BgmVolume);
        }

        /// <summary>
        /// 現在のSlider値を保存する。
        /// 実際のPlayerPrefs保存処理はGameSettingsStoreに任せる。
        /// </summary>
        private void SaveSettings()
        {
            GameSettingsStore.Save(CurrentSettings);
        }

        /// <summary>
        /// マウス感度Sliderが変更されたときの処理。
        /// </summary>
        private void HandleMouseSensitivityChanged(float value)
        {
            UpdateMouseSensitivityText(value);
            SaveSettings();
        }

        /// <summary>
        /// スティック感度Sliderが変更されたときの処理。
        /// </summary>
        private void HandleStickSensitivityChanged(float value)
        {
            UpdateStickSensitivityText(value);
            SaveSettings();
        }

        /// <summary>
        /// キーボード・マウス操作用デッドゾーンSliderが変更されたときの処理。
        /// </summary>
        private void HandleKeyMouseDeadZoneChanged(float value)
        {
            UpdateKeyMouseDeadZoneText(value);
            SaveSettings();
        }

        /// <summary>
        /// スティック操作用デッドゾーンSliderが変更されたときの処理。
        /// </summary>
        private void HandleStickDeadZoneChanged(float value)
        {
            UpdateStickDeadZoneText(value);
            SaveSettings();
        }

        /// <summary>
        /// 効果音音量Sliderが変更されたときの処理。
        /// </summary>
        private void HandleSeVolumeChanged(float value)
        {
            UpdateSeVolumeText(value);
            SaveSettings();
        }

        /// <summary>
        /// BGM音量Sliderが変更されたときの処理。
        /// </summary>
        private void HandleBgmVolumeChanged(float value)
        {
            UpdateBgmVolumeText(value);
            SaveSettings();
        }

        /// <summary>
        /// すべてのSlider値を表示テキストへ反映する。
        /// </summary>
        private void UpdateAllTexts()
        {
            UpdateMouseSensitivityText(_mouseSensitivitySlider.value);
            UpdateStickSensitivityText(_stickSensitivitySlider.value);
            UpdateKeyMouseDeadZoneText(_keyMouseDeadZoneSlider.value);
            UpdateStickDeadZoneText(_stickDeadZoneSlider.value);
            UpdateSeVolumeText(_seVolumeSlider.value);
            UpdateBgmVolumeText(_bgmVolumeSlider.value);
        }

        /// <summary>
        /// マウス感度の表示を更新する。
        /// </summary>
        private void UpdateMouseSensitivityText(float value)
        {
            _mouseSensitivityValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// スティック感度の表示を更新する。
        /// </summary>
        private void UpdateStickSensitivityText(float value)
        {
            _stickSensitivityValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// キーボード・マウス操作用デッドゾーンの表示を更新する。
        /// </summary>
        private void UpdateKeyMouseDeadZoneText(float value)
        {
            _keyMouseDeadZoneValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// スティック操作用デッドゾーンの表示を更新する。
        /// </summary>
        private void UpdateStickDeadZoneText(float value)
        {
            _stickDeadZoneValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// 効果音音量の表示を更新する。
        /// </summary>
        private void UpdateSeVolumeText(float value)
        {
            _seVolumeValueText.text = FormatPercentText(value);
        }

        /// <summary>
        /// BGM音量の表示を更新する。
        /// </summary>
        private void UpdateBgmVolumeText(float value)
        {
            _bgmVolumeValueText.text = FormatPercentText(value);
        }

        /// <summary>
        /// 0.0～1.0の値を、0%～100%の文字列へ整形する。
        /// </summary>
        private string FormatPercentText(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
