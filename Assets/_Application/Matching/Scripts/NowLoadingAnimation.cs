using UnityEngine;

using UnityEngine.UI;

namespace Uraty.Application.Matching
{
    public class NowLoadingAnimation : MonoBehaviour
    {
        [SerializeField] private Image _targetImage;
        [SerializeField] private Sprite[] _frames = new Sprite[12];
        [SerializeField] private float _frameRate = 4.0f;
        [SerializeField] private bool _playOnStart = true;
        [SerializeField] private bool _loop = true;

        private float _timer;
        private int _currentFrame;
        private bool _isPlaying;

        private void Awake()
        {
            if (_targetImage == null)
            {
                _targetImage = GetComponent<Image>();
            }
        }

        private void Start()
        {
            if (_playOnStart)
            {
                Play();
            }
        }

        private void Update()
        {
            if (!_isPlaying || _targetImage == null || _frames == null || _frames.Length == 0)
            {
                return;
            }

            if (_frameRate <= 0.0f)
            {
                return;
            }

            _timer += Time.deltaTime;

            float frameTime = 1.0f / _frameRate;
            if (_timer < frameTime)
            {
                return;
            }

            _timer -= frameTime;
            AdvanceFrame();
        }

        public void Play()
        {
            if (_frames == null || _frames.Length == 0)
            {
                return;
            }

            _isPlaying = true;
            _currentFrame = 0;
            _timer = 0.0f;
            ApplyFrame();
        }

        public void Stop()
        {
            _isPlaying = false;
        }

        public void Pause()
        {
            _isPlaying = false;
        }

        public void Resume()
        {
            if (_frames == null || _frames.Length == 0)
            {
                return;
            }

            _isPlaying = true;
        }

        private void AdvanceFrame()
        {
            _currentFrame++;

            if (_currentFrame >= _frames.Length)
            {
                if (_loop)
                {
                    _currentFrame = 0;
                }
                else
                {
                    _currentFrame = _frames.Length - 1;
                    _isPlaying = false;
                }
            }

            ApplyFrame();
        }

        private void ApplyFrame()
        {
            Sprite frame = _frames[_currentFrame];

            if (frame != null)
            {
                _targetImage.sprite = frame;
            }
        }
    }
}
