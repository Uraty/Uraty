using UnityEngine;

namespace Uraty.Shared.Setting
{
    /// <summary>
    /// ゲーム設定をPlayerPrefsへ保存・読み込みするクラス。
    /// staticな現在値は持たず、必要なタイミングでLoadして値を返す。
    /// </summary>
    public static class GameSettingsStore
    {
        // PlayerPrefsに保存するキー名。
        private const string MouseSensitivityKey = "MouseSensitivity";
        private const string StickSensitivityKey = "StickSensitivity";
        private const string KeyMouseDeadZoneKey = "KeyMouseDeadZone";
        private const string StickDeadZoneKey = "StickDeadZone";
        private const string SeVolumeKey = "SeVolume";
        private const string BgmVolumeKey = "BgmVolume";

        // 設定がまだ保存されていない場合に使用する初期値。
        private const float DefaultMouseSensitivityScale = 1.0f;
        private const float DefaultStickSensitivityScale = 1.0f;
        private const float DefaultKeyMouseDeadZoneRatio = 0.0f;
        private const float DefaultStickDeadZoneRatio = 0.2f;
        private const float DefaultSeVolumeRatio = 1.0f;
        private const float DefaultBgmVolumeRatio = 1.0f;

        /// <summary>
        /// PlayerPrefsから設定値を読み込む。
        /// まだ保存されていない項目は初期値を使用する。
        /// </summary>
        public static GameSettingsData Load()
        {
            return new GameSettingsData(
                PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivityScale),
                PlayerPrefs.GetFloat(StickSensitivityKey, DefaultStickSensitivityScale),
                PlayerPrefs.GetFloat(KeyMouseDeadZoneKey, DefaultKeyMouseDeadZoneRatio),
                PlayerPrefs.GetFloat(StickDeadZoneKey, DefaultStickDeadZoneRatio),
                PlayerPrefs.GetFloat(SeVolumeKey, DefaultSeVolumeRatio),
                PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolumeRatio)
            );
        }

        /// <summary>
        /// 指定された設定値をPlayerPrefsに保存する。
        /// </summary>
        public static void Save(GameSettingsData settings)
        {
            PlayerPrefs.SetFloat(MouseSensitivityKey, settings.MouseSensitivity);
            PlayerPrefs.SetFloat(StickSensitivityKey, settings.StickSensitivity);
            PlayerPrefs.SetFloat(KeyMouseDeadZoneKey, settings.KeyMouseDeadZone);
            PlayerPrefs.SetFloat(StickDeadZoneKey, settings.StickDeadZone);
            PlayerPrefs.SetFloat(SeVolumeKey, settings.SeVolume);
            PlayerPrefs.SetFloat(BgmVolumeKey, settings.BgmVolume);

            PlayerPrefs.Save();
        }
    }
}
