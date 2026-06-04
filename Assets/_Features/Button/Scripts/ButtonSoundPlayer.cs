using System.Collections;

using UnityEngine;

using Uraty.Systems.Sound;

namespace Uraty.Features.Button
{
    public sealed class ButtonSoundPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("押されたときに流すサウンド")]
        private SoundManager _soundManager;

        [SerializeField, Tooltip("実際に流すサウンド")]
        private AudioClip _audioClip;

        [SerializeField, Tooltip("対象のボタン")]
        private ButtonSystem _buttonController;

        private Coroutine _soundCoroutine;

        private void Awake()
        {
            if (_soundManager == null)
            {
                Debug.LogError("SoundManagerがアタッチされていません。", this);
            }

            if (_audioClip == null)
            {
                Debug.LogError("AudioClipがアタッチされていません。", this);
            }

            if (_buttonController == null)
            {
                Debug.LogError("ButtonSystemがアタッチされていません。", this);
            }
        }

        private void Start()
        {
            if (_buttonController == null)
            {
                return;
            }

            _buttonController.AddPressedRequestedListener(PlaySound);
        }

        private void OnDisable()
        {
            if (_buttonController == null)
            {
                return;
            }

            _buttonController.RemovePressedRequestedListener(PlaySound);
        }

        private void PlaySound()
        {
            if (_soundCoroutine != null)
            {
                StopCoroutine(_soundCoroutine);
                _soundCoroutine = null;
            }

            if (_buttonController == null)
            {
                return;
            }

            if (_soundManager == null || _audioClip == null)
            {
                _buttonController.NotifyPressedSequenceCompleted();
                return;
            }

            _soundManager.PlaySE(_audioClip, 1.0f);
            _soundCoroutine = StartCoroutine(WaitSoundEnd());
        }

        private IEnumerator WaitSoundEnd()
        {
            yield return new WaitForSecondsRealtime(_audioClip.length);

            _soundCoroutine = null;

            if (_buttonController == null)
            {
                yield break;
            }

            _buttonController.NotifyPressedSequenceCompleted();
        }
    }
}
