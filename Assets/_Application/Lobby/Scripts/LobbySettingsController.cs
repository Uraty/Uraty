using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビーの設定画面を管理するクラス。
    /// 感度、デッドゾーン、SE音量、BGM音量をSliderで変更し、PlayerPrefsに保存する。
    /// </summary>
    public sealed class LobbySettingsController : MonoBehaviour
    {
        [Header("Panels")]
        // ロビー通常画面。
        [SerializeField] private GameObject _mainPanel;

        // ロビー中央のキャラ表示などを含むPanel。
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

        // キーマウ用デッドゾーン。
        [SerializeField] private Slider _keyMouseDeadZoneSlider;

        // スティック用デッドゾーン。
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

        // キーマウ用デッドゾーンの現在値表示。
        [SerializeField] private TextMeshProUGUI _keyMouseDeadZoneValueText;

        // スティック用デッドゾーンの現在値表示。
        [SerializeField] private TextMeshProUGUI _stickDeadZoneValueText;

        // 効果音音量の現在値表示。
        [SerializeField] private TextMeshProUGUI _seVolumeValueText;

        // BGM音量の現在値表示。
        [SerializeField] private TextMeshProUGUI _bgmVolumeValueText;

        // PlayerPrefsに保存するときのキー名。
        private const string MouseSensitivityKey = "MouseSensitivity";
        private const string StickSensitivityKey = "StickSensitivity";
        private const string KeyMouseDeadZoneKey = "KeyMouseDeadZone";
        private const string StickDeadZoneKey = "StickDeadZone";
        private const string SeVolumeKey = "SeVolume";
        private const string BgmVolumeKey = "BgmVolume";

        private void Awake()
        {
            // 保存済み設定を読み込み、Sliderに反映する。
            LoadSettings();

            // Sliderの値をTextにも反映する。
            RefreshAllTexts();

            // 設定画面の開閉ボタンを登録する。
            _openSettingButton.onClick.AddListener(OpenSettingPanel);
            _closeSettingButton.onClick.AddListener(CloseSettingPanel);

            // 各Sliderの値が変わったときに、保存と表示更新を行う。
            _mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            _stickSensitivitySlider.onValueChanged.AddListener(OnStickSensitivityChanged);
            _keyMouseDeadZoneSlider.onValueChanged.AddListener(OnKeyMouseDeadZoneChanged);
            _stickDeadZoneSlider.onValueChanged.AddListener(OnStickDeadZoneChanged);
            _seVolumeSlider.onValueChanged.AddListener(OnSeVolumeChanged);
            _bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);

            // 起動時は設定画面を閉じた状態にする。
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
        /// 設定画面を閉じて、通常ロビー画面に戻る。
        /// </summary>
        private void CloseSettingPanel()
        {
            _settingPanel.SetActive(false);
            _mainPanel.SetActive(true);
            _modelPanel.SetActive(true);
        }

        /// <summary>
        /// PlayerPrefsから保存済み設定を読み込み、Sliderに反映する。
        /// まだ保存されていない場合は、第二引数の初期値を使う。
        /// </summary>
        private void LoadSettings()
        {
            _mouseSensitivitySlider.value = PlayerPrefs.GetFloat(MouseSensitivityKey, 1.0f);
            _stickSensitivitySlider.value = PlayerPrefs.GetFloat(StickSensitivityKey, 1.0f);
            _keyMouseDeadZoneSlider.value = PlayerPrefs.GetFloat(KeyMouseDeadZoneKey, 0.0f);
            _stickDeadZoneSlider.value = PlayerPrefs.GetFloat(StickDeadZoneKey, 0.2f);
            _seVolumeSlider.value = PlayerPrefs.GetFloat(SeVolumeKey, 1.0f);
            _bgmVolumeSlider.value = PlayerPrefs.GetFloat(BgmVolumeKey, 1.0f);
        }

        /// <summary>
        /// PlayerPrefsにfloat値を保存する。
        /// </summary>
        private void SaveFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);

            // 明示的に保存を確定する。
            PlayerPrefs.Save();
        }

        /// <summary>
        /// マウス感度Sliderが変更されたときの処理。
        /// </summary>
        private void OnMouseSensitivityChanged(float value)
        {
            SaveFloat(MouseSensitivityKey, value);
            _mouseSensitivityValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// スティック感度Sliderが変更されたときの処理。
        /// </summary>
        private void OnStickSensitivityChanged(float value)
        {
            SaveFloat(StickSensitivityKey, value);
            _stickSensitivityValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// キーマウ用デッドゾーンSliderが変更されたときの処理。
        /// </summary>
        private void OnKeyMouseDeadZoneChanged(float value)
        {
            SaveFloat(KeyMouseDeadZoneKey, value);
            _keyMouseDeadZoneValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// スティック用デッドゾーンSliderが変更されたときの処理。
        /// </summary>
        private void OnStickDeadZoneChanged(float value)
        {
            SaveFloat(StickDeadZoneKey, value);
            _stickDeadZoneValueText.text = value.ToString("0.00");
        }

        /// <summary>
        /// 効果音音量Sliderが変更されたときの処理。
        /// </summary>
        private void OnSeVolumeChanged(float value)
        {
            SaveFloat(SeVolumeKey, value);
            _seVolumeValueText.text = ToPercentText(value);
        }

        /// <summary>
        /// BGM音量Sliderが変更されたときの処理。
        /// </summary>
        private void OnBgmVolumeChanged(float value)
        {
            SaveFloat(BgmVolumeKey, value);
            _bgmVolumeValueText.text = ToPercentText(value);
        }

        /// <summary>
        /// 現在のSlider値を、すべてのValueTextに反映する。
        /// </summary>
        private void RefreshAllTexts()
        {
            _mouseSensitivityValueText.text = _mouseSensitivitySlider.value.ToString("0.00");
            _stickSensitivityValueText.text = _stickSensitivitySlider.value.ToString("0.00");
            _keyMouseDeadZoneValueText.text = _keyMouseDeadZoneSlider.value.ToString("0.00");
            _stickDeadZoneValueText.text = _stickDeadZoneSlider.value.ToString("0.00");
            _seVolumeValueText.text = ToPercentText(_seVolumeSlider.value);
            _bgmVolumeValueText.text = ToPercentText(_bgmVolumeSlider.value);
        }

        /// <summary>
        /// 0.0～1.0の値を、0%～100%の文字列に変換する。
        /// </summary>
        private string ToPercentText(float value)
        {
            return Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
