using UnityEngine;

namespace Uraty.Features.Sound
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance
        {
            get; private set;
        }

        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioSource bgmSource;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void PlaySE(AudioClip clip, float volume = 1.0f)
        {
            if (clip == null || seSource == null)
                return;

            seSource.PlayOneShot(clip, volume);
        }

        public void PlayBGM(AudioClip clip, float volume = 1.0f, bool loop = true)
        {
            if (clip == null || bgmSource == null)
                return;

            bgmSource.clip = clip;
            bgmSource.volume = volume;
            bgmSource.loop = loop;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource == null)
                return;

            bgmSource.Stop();
        }
    }
}
