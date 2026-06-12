using UnityEngine;
using Uraty.Systems.Sound;

namespace Uraty.Application.Title
{
    public class PlayIntro : MonoBehaviour
    {
        [SerializeField] private SoundManager _soundManager;
        [SerializeField] private AudioClip _introClip;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if (_soundManager == null)
            {
                Debug.LogError("SoundManagerがアタッチされていません。", this);
                return;
            }
            if (_introClip == null)
            {
                Debug.LogError("AudioClipがアタッチされていません。", this);
                return;
            }

            _soundManager.PlaySE(_introClip);
        }
    }
}
