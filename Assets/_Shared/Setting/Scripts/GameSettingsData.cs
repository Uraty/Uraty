namespace Uraty.Shared.Setting
{
    /// <summary>
    /// ゲーム内で使用する設定値をまとめたデータ。
    /// PlayerPrefsから読み込んだ値や、設定画面のSlider値を受け渡すために使用する。
    /// </summary>
    public struct GameSettingsData
    {
        /// <summary>
        /// マウス操作時の感度。
        /// </summary>
        public float MouseSensitivity
        {
            get;
        }

        /// <summary>
        /// スティック操作時の感度。
        /// </summary>
        public float StickSensitivity
        {
            get;
        }

        /// <summary>
        /// キーボード・マウス操作用のデッドゾーン。
        /// </summary>
        public float KeyMouseDeadZone
        {
            get;
        }

        /// <summary>
        /// スティック操作用のデッドゾーン。
        /// </summary>
        public float StickDeadZone
        {
            get;
        }

        /// <summary>
        /// 効果音の音量。
        /// 0.0～1.0の範囲を想定する。
        /// </summary>
        public float SeVolume
        {
            get;
        }

        /// <summary>
        /// BGMの音量。
        /// 0.0～1.0の範囲を想定する。
        /// </summary>
        public float BgmVolume
        {
            get;
        }

        /// <summary>
        /// 設定値をまとめて初期化する。
        /// </summary>
        public GameSettingsData(
            float mouseSensitivity,
            float stickSensitivity,
            float keyMouseDeadZone,
            float stickDeadZone,
            float seVolume,
            float bgmVolume)
        {
            MouseSensitivity = mouseSensitivity;
            StickSensitivity = stickSensitivity;
            KeyMouseDeadZone = keyMouseDeadZone;
            StickDeadZone = stickDeadZone;
            SeVolume = seVolume;
            BgmVolume = bgmVolume;
        }
    }
}
