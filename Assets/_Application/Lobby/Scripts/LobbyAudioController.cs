using UnityEngine;
using UnityEngine.UI;

using Uraty.Shared.Setting;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビー画面のBGM / SE再生を管理するクラス。
    /// </summary>
    public sealed class LobbyAudioController : MonoBehaviour
    {
        [Header("Audio Source")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _seSource;

        [Header("BGM")]
        [SerializeField] private AudioClip _lobbyBgmClip;

        [Header("SE")]
        [SerializeField] private AudioClip _buttonPressedSeClip;
        [SerializeField] private AudioClip _roleSelectedSeClip;

        [Header("Targets")]
        [SerializeField] private Button[] _buttonPressedTargets;

        private void Awake()
        {
            SetupAudioSources();
        }

        private void OnEnable()
        {
            AddButtonListeners();
        }

        private void Start()
        {
            ApplyVolumeSettings();
            PlayLobbyBgm();
        }

        private void OnDisable()
        {
            RemoveButtonListeners();
        }

        /// <summary>
        /// AudioSourceの基本設定を行う。
        /// UI用の音なので、位置に影響されない2D音声として扱う。
        /// </summary>
        private void SetupAudioSources()
        {
            if (_bgmSource != null)
            {
                _bgmSource.spatialBlend = 0.0f;
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
            }

            if (_seSource != null)
            {
                _seSource.spatialBlend = 0.0f;
                _seSource.loop = false;
                _seSource.playOnAwake = false;
            }
        }

        /// <summary>
        /// PlayerPrefsに保存されている音量設定をAudioSourceへ反映する。
        /// </summary>
        public void ApplyVolumeSettings()
        {
            GameSettingsData settings = GameSettingsStore.Load();

            if (_bgmSource != null)
            {
                _bgmSource.volume = Mathf.Clamp01(settings.BgmVolume);
            }

            if (_seSource != null)
            {
                _seSource.volume = Mathf.Clamp01(settings.SeVolume);
            }
        }

        /// <summary>
        /// ロビーBGMを再生する。
        /// </summary>
        private void PlayLobbyBgm()
        {
            if (_bgmSource == null)
            {
                Debug.LogWarning("BGM用AudioSourceが設定されていません。");
                return;
            }

            if (_lobbyBgmClip == null)
            {
                Debug.LogWarning("Lobby BGM Clipが設定されていません。");
                return;
            }

            _bgmSource.clip = _lobbyBgmClip;
            _bgmSource.loop = true;

            if (_bgmSource.isPlaying)
            {
                return;
            }

            _bgmSource.Play();
        }

        /// <summary>
        /// ボタン押下SEを鳴らすボタンを登録する。
        /// </summary>
        private void AddButtonListeners()
        {
            if (_buttonPressedTargets == null)
            {
                return;
            }

            foreach (Button button in _buttonPressedTargets)
            {
                if (button == null)
                {
                    continue;
                }

                button.onClick.AddListener(PlayButtonPressedSe);
            }
        }

        /// <summary>
        /// ボタン押下SEを鳴らすボタンの登録を解除する。
        /// </summary>
        private void RemoveButtonListeners()
        {
            if (_buttonPressedTargets == null)
            {
                return;
            }

            foreach (Button button in _buttonPressedTargets)
            {
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveListener(PlayButtonPressedSe);
            }
        }

        /// <summary>
        /// ボタン押下SEを再生する。
        /// CharacterSelectSceneなど、別Controllerからも呼ぶ。
        /// </summary>
        public void PlayButtonPressedSe()
        {
            PlaySe(_buttonPressedSeClip);
        }

        /// <summary>
        /// ロール / キャラ選択SEを再生する。
        /// </summary>
        public void PlayRoleSelectedSe()
        {
            PlaySe(_roleSelectedSeClip);
        }

        /// <summary>
        /// SEを再生する。
        /// 再生直前にPlayerPrefsのSE音量を反映する。
        /// </summary>
        private void PlaySe(AudioClip clip)
        {
            if (_seSource == null)
            {
                Debug.LogWarning("SE用AudioSourceが設定されていません。");
                return;
            }

            if (clip == null)
            {
                return;
            }

            GameSettingsData settings = GameSettingsStore.Load();
            _seSource.volume = Mathf.Clamp01(settings.SeVolume);

            _seSource.PlayOneShot(clip);
        }
    }
}
