using System.Collections;

using UnityEngine;
using UnityEngine.Events;

namespace Uraty.Feature.Button
{
    public sealed class ButtonScaling : MonoBehaviour
    {
        [SerializeField, Tooltip("判定用のイメージ")]
        private RectTransform _rectTransformHitbox;

        [SerializeField, Tooltip("見た目用のイメージ")]
        private RectTransform _rectTransformVisual;

        [SerializeField, Tooltip("ボタンに表示されてるテキスト")]
        private RectTransform _rectTransformText;

        [SerializeField, Tooltip("ボタンシステム")]
        private ButtonSystem _buttonSystem;

        [SerializeField, Tooltip("カーソルが重なった時のスケール倍率")]
        private float _hoverScale = 0.92f;

        [SerializeField, Tooltip("決定された時のスケール倍率")]
        private float _pressedScale = 0.82f;

        [SerializeField, Tooltip("通常時の追従速度")]
        private float _scaleSpeed = 12.0f;

        [SerializeField, Tooltip("決定演出の秒数")]
        private float _pressedDurationSeconds = 0.08f;

        [SerializeField, Tooltip("決定演出後に実行する処理")]
        private UnityEvent _pressedAfterScaling = new UnityEvent();

        private Vector3 _defaultScaleVisual;
        private Vector3 _defaultScaleText;

        private Coroutine _pressedCoroutine;
        private bool _isPressing;
        private bool _isPadCursorInside;

        public RectTransform HitboxRectTransform => _rectTransformHitbox;

        private void Start()
        {
            if (_buttonSystem == null)
            {
                Debug.LogError($"{nameof(ButtonScaling)}: ButtonSystemが設定されていません。", this);
                return;
            }

            if (_rectTransformHitbox == null)
            {
                Debug.LogError($"{nameof(ButtonScaling)}: クリック判定対象が設定されていません。", this);
                return;
            }

            if (_rectTransformVisual == null)
            {
                Debug.LogError($"{nameof(ButtonScaling)}: 見た目用のイメージが設定されていません。", this);
                return;
            }

            if (_rectTransformText == null)
            {
                Debug.LogError($"{nameof(ButtonScaling)}: ボタンに表示されてるテキストが設定されていません。", this);
                return;
            }

            _defaultScaleVisual = _rectTransformVisual.localScale;
            _defaultScaleText = _rectTransformText.localScale;

            SyncHitboxToVisual();

            _buttonSystem.AddPressedRequestedListener(HandlePressed);
        }

        private void OnDestroy()
        {
            if (_buttonSystem != null)
            {
                _buttonSystem.RemovePressedRequestedListener(HandlePressed);
            }
        }

        private void Update()
        {
            if (_buttonSystem == null || _rectTransformVisual == null || _rectTransformText == null || _isPressing)
            {
                return;
            }

            bool shouldHover = _buttonSystem.IsPointerInside || _isPadCursorInside;

            Vector3 targetScaleVisual = shouldHover
                ? _defaultScaleVisual * _hoverScale
                : _defaultScaleVisual;

            Vector3 targetScaleText = shouldHover
                ? _defaultScaleText * _hoverScale
                : _defaultScaleText;

            float rate = EaseOutCubic(Time.unscaledDeltaTime * _scaleSpeed);

            _rectTransformVisual.localScale = Vector3.Lerp(
                _rectTransformVisual.localScale,
                targetScaleVisual,
                rate);

            _rectTransformText.localScale = Vector3.Lerp(
                _rectTransformText.localScale,
                targetScaleText,
                rate);
        }

        public void SetPadCursorInside(bool isInside)
        {
            _isPadCursorInside = isInside;
        }

        public void PlayPressedByPadCursor()
        {
            HandlePressed();
        }

        private void HandlePressed()
        {
            if (_pressedCoroutine != null)
            {
                StopCoroutine(_pressedCoroutine);
            }

            _pressedCoroutine = StartCoroutine(PlayPressedScaling());
        }

        private IEnumerator PlayPressedScaling()
        {
            _isPressing = true;

            yield return ScalePairTo(
                _defaultScaleVisual * _pressedScale,
                _defaultScaleText * _pressedScale,
                _pressedDurationSeconds);

            yield return ScalePairTo(
                _defaultScaleVisual,
                _defaultScaleText,
                _pressedDurationSeconds);

            _rectTransformVisual.localScale = _defaultScaleVisual;
            _rectTransformText.localScale = _defaultScaleText;

            _isPressing = false;
            _pressedCoroutine = null;

            _buttonSystem.NotifyPressedSequenceCompleted();
            _pressedAfterScaling.Invoke();
        }

        private IEnumerator ScalePairTo(Vector3 targetScaleVisual, Vector3 targetScaleText, float durationSeconds)
        {
            Vector3 startScaleVisual = _rectTransformVisual.localScale;
            Vector3 startScaleText = _rectTransformText.localScale;

            float elapsedSeconds = 0.0f;

            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.unscaledDeltaTime;

                float rate = Mathf.Clamp01(elapsedSeconds / durationSeconds);
                float easedRate = EaseOutCubic(rate);

                _rectTransformVisual.localScale =
                    Vector3.Lerp(startScaleVisual, targetScaleVisual, easedRate);

                _rectTransformText.localScale =
                    Vector3.Lerp(startScaleText, targetScaleText, easedRate);

                yield return null;
            }

            _rectTransformVisual.localScale = targetScaleVisual;
            _rectTransformText.localScale = targetScaleText;
        }

        /// <summary>
        /// 判定用のイメージを見た目用のイメージと同じ位置・サイズにする。
        /// </summary>
        private void SyncHitboxToVisual()
        {
            _rectTransformHitbox.anchorMin = _rectTransformVisual.anchorMin;
            _rectTransformHitbox.anchorMax = _rectTransformVisual.anchorMax;
            _rectTransformHitbox.pivot = _rectTransformVisual.pivot;
            _rectTransformHitbox.anchoredPosition = _rectTransformVisual.anchoredPosition;
            _rectTransformHitbox.sizeDelta = _rectTransformVisual.sizeDelta;
        }

        private float EaseOutCubic(float rate)
        {
            float invertedRate = 1.0f - Mathf.Clamp01(rate);
            return 1.0f - invertedRate * invertedRate * invertedRate;
        }
    }
}
