using TMPro;

using UnityEngine;

namespace Uraty.Feature.Timer
{
    public class CountDown : MonoBehaviour
    {
        [SerializeField] private float _durationSeconds = 180f;
        [SerializeField] private TextMeshProUGUI _timerText;

        private float _remainingSeconds;
        private bool _isRunning;

        public bool IsRunning => _isRunning;
        public float RemainingSeconds => _remainingSeconds;

        private void Awake()
        {
            _remainingSeconds = _durationSeconds;
            _isRunning = false;
        }

        private void Start()
        {
            StartTimer();
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            if (_remainingSeconds > 0f)
            {
                _remainingSeconds -= Time.deltaTime;
                UpdateTimerDisplay(_remainingSeconds);
                return;
            }

            _remainingSeconds = 0f;
            _isRunning = false;
            HandleTimerCompleted();
        }

        public void StartTimer()
        {
            _isRunning = true;
        }

        public void StopTimer()
        {
            _isRunning = false;
        }

        public void ResetTimer()
        {
            _remainingSeconds = _durationSeconds;
            UpdateTimerDisplay(_remainingSeconds);
        }

        private void UpdateTimerDisplay(float remainingSeconds)
        {
            var minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            var seconds = Mathf.FloorToInt(remainingSeconds % 60f);

            _timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private void HandleTimerCompleted()
        {
            Debug.Log("Timer completed");
        }
    }
}
