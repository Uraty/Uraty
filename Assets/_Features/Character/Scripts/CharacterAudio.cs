using System;

using UnityEngine;

namespace Uraty.Features.Character
{
    /// <summary>
    /// キャラクターで使用するSEを役職ごとに管理し、再生するコンポーネント。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAudio : MonoBehaviour
    {
        [Header("Audio Source")]
        [Tooltip("SE再生に使用するAudioSource。未設定の場合は同じGameObjectから取得し、存在しなければ自動で追加する")]
        [SerializeField]
        private AudioSource _audioSource;

        [Tooltip("Initializeが呼ばれる前に使用する役職")]
        [SerializeField]
        private AudioRoleType _defaultRoleType;

        [Header("Role SE")]
        [SerializeField]
        private RoleAudioSetting[] _roleAudioSettings = Array.Empty<RoleAudioSetting>();

        [SerializeField, Range(0f, 1f)]
        private float _attackVolume = 1f;

        [SerializeField, Range(0f, 1f)]
        private float _reloadVolume = 1f;

        [SerializeField, Range(0f, 1f)]
        private float _superVolume = 1f;

        [SerializeField, Range(0f, 1f)]
        private float _superHealVolume = 1f;

        [Header("Common SE")]
        [SerializeField]
        private AudioClip _deadSe;

        [SerializeField, Range(0f, 1f)]
        private float _deadVolume = 1f;

        [SerializeField]
        private AudioClip _healSe;

        [SerializeField, Range(0f, 1f)]
        private float _healVolume = 1f;

        [SerializeField]
        private AudioClip _noAmmoSe;

        [SerializeField, Range(0f, 1f)]
        private float _noAmmoVolume = 1f;

        [SerializeField]
        private AudioClip _respawnSe;

        [SerializeField, Range(0f, 1f)]
        private float _respawnVolume = 1f;

        [SerializeField]
        private AudioClip _superReadySe;

        [SerializeField, Range(0f, 1f)]
        private float _superReadyVolume = 1f;

        private RoleAudioSetting _currentRoleAudioSetting;

        private void Awake()
        {
            EnsureAudioSource();
            Initialize((int)_defaultRoleType);
        }

        public void Initialize(int roleTypeValue)
        {
            _currentRoleAudioSetting =
                FindRoleAudioSetting(roleTypeValue);
        }

        public void PlayAttack()
        {
            PlayOneShot(
                _currentRoleAudioSetting?.AttackSe,
                _attackVolume);
        }

        public void PlayReload()
        {
            PlayOneShot(
                _currentRoleAudioSetting?.ReloadSe,
                _reloadVolume);
        }

        public void PlaySuper()
        {
            PlayOneShot(
                _currentRoleAudioSetting?.SuperSe,
                _superVolume);
        }

        public void PlaySuperHeal()
        {
            AudioClip superHealSe =
                _currentRoleAudioSetting?.SuperHealSe;

            if (superHealSe != null)
            {
                PlayOneShot(
                    superHealSe,
                    _superHealVolume);

                return;
            }

            PlayHeal();
        }

        public void PlayDead()
        {
            PlayDetached(
                _deadSe,
                _deadVolume);
        }

        public void PlayHeal()
        {
            PlayOneShot(
                _healSe,
                _healVolume);
        }

        public void PlayNoAmmo()
        {
            PlayOneShot(
                _noAmmoSe,
                _noAmmoVolume);
        }

        public void PlayRespawn()
        {
            PlayDetached(
                _respawnSe,
                _respawnVolume);
        }

        public void PlaySuperReady()
        {
            PlayOneShot(
                _superReadySe,
                _superReadyVolume);
        }

        private RoleAudioSetting FindRoleAudioSetting(int roleTypeValue)
        {
            if (_roleAudioSettings == null)
            {
                return null;
            }

            for (int i = 0; i < _roleAudioSettings.Length; i++)
            {
                RoleAudioSetting setting =
                    _roleAudioSettings[i];

                if (setting == null)
                {
                    continue;
                }

                if (setting.RoleTypeValue == roleTypeValue)
                {
                    return setting;
                }
            }

            return null;
        }

        private void PlayOneShot(
            AudioClip clip,
            float volume)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSource();

            if (_audioSource == null)
            {
                return;
            }

            _audioSource.PlayOneShot(
                clip,
                Mathf.Clamp01(volume));
        }

        private void PlayDetached(
            AudioClip clip,
            float volume)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSource();

            GameObject temporaryAudioObject =
                new GameObject($"{name}_{clip.name}");

            temporaryAudioObject.transform.position =
                transform.position;

            AudioSource temporaryAudioSource =
                temporaryAudioObject.AddComponent<AudioSource>();

            CopyAudioSourceSettings(
                _audioSource,
                temporaryAudioSource);

            temporaryAudioSource.playOnAwake = false;
            temporaryAudioSource.loop = false;
            temporaryAudioSource.clip = clip;
            temporaryAudioSource.volume =
                (_audioSource != null
                    ? _audioSource.volume
                    : 1f)
                * Mathf.Clamp01(volume);

            temporaryAudioSource.Play();

            float absolutePitch =
                Mathf.Max(
                    0.01f,
                    Mathf.Abs(temporaryAudioSource.pitch));

            Destroy(
                temporaryAudioObject,
                clip.length / absolutePitch + 0.1f);
        }

        private void EnsureAudioSource()
        {
            if (_audioSource == null
                && !TryGetComponent(out _audioSource))
            {
                _audioSource =
                    gameObject.AddComponent<AudioSource>();
            }

            if (_audioSource != null)
            {
                _audioSource.playOnAwake = false;
            }
        }

        private static void CopyAudioSourceSettings(
            AudioSource source,
            AudioSource destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.outputAudioMixerGroup =
                source.outputAudioMixerGroup;

            destination.mute = source.mute;
            destination.bypassEffects = source.bypassEffects;
            destination.bypassListenerEffects = source.bypassListenerEffects;
            destination.bypassReverbZones = source.bypassReverbZones;
            destination.priority = source.priority;
            destination.pitch = source.pitch;
            destination.panStereo = source.panStereo;
            destination.spatialBlend = source.spatialBlend;
            destination.reverbZoneMix = source.reverbZoneMix;
            destination.dopplerLevel = source.dopplerLevel;
            destination.spread = source.spread;
            destination.rolloffMode = source.rolloffMode;
            destination.minDistance = source.minDistance;
            destination.maxDistance = source.maxDistance;
        }


        private enum AudioRoleType
        {
            Fighter = 0,
            Sniper = 1,
            Attacker = 2,
            Assassin = 3,
            Healer = 4
        }

        [Serializable]
        private sealed class RoleAudioSetting
        {
            [SerializeField]
            private AudioRoleType _roleType;

            [Tooltip("Inspector上で識別するための名前")]
            [SerializeField]
            private string _displayName;

            [SerializeField]
            private AudioClip _attackSe;

            [SerializeField]
            private AudioClip _reloadSe;

            [SerializeField]
            private AudioClip _superSe;

            [Tooltip("必殺技による回復が実際に成立したときのSE。Healer用")]
            [SerializeField]
            private AudioClip _superHealSe;

            public int RoleTypeValue => (int)_roleType;
            public AudioClip AttackSe => _attackSe;
            public AudioClip ReloadSe => _reloadSe;
            public AudioClip SuperSe => _superSe;
            public AudioClip SuperHealSe => _superHealSe;
        }
    }
}
