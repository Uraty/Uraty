using System;

using TMPro;

using UnityEngine;

namespace Uraty.Features.Timer
{
    /// <summary>
    /// カウントダウン時間を表示する UI コンポーネントです。
    /// 秒数の保持や減算は行わず、表示だけを担当します。
    /// </summary>
    public sealed class CountDown : MonoBehaviour
    {
        /// <summary>
        /// 残り時間を表示する TextMeshProUGUI です。
        /// </summary>
        [SerializeField]
        private TextMeshProUGUI _timerText;

        private void Reset()
        {
            _timerText =
                GetComponent<TextMeshProUGUI>();
        }

        private void Awake()
        {
            if (_timerText == null)
            {
                _timerText =
                    GetComponent<TextMeshProUGUI>();
            }

            if (_timerText == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(TextMeshProUGUI)} が設定されていません。");
            }
        }

        /// <summary>
        /// 指定された残り秒数をタイマー表示へ反映します。
        /// </summary>
        /// <param name="remainingSeconds">表示する残り秒数です。</param>
        public void SetRemainingSeconds(
            float remainingSeconds)
        {
            float clampedSeconds =
                Mathf.Max(
                    0f,
                    remainingSeconds);

            int minutes =
                Mathf.FloorToInt(
                    clampedSeconds / 60f);

            int seconds =
                Mathf.FloorToInt(
                    clampedSeconds % 60f);

            _timerText.text =
                $"{minutes:00}:{seconds:00}";
        }
    }
}
