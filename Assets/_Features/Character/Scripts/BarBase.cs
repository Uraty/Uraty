using UnityEngine;
using UnityEngine.UI;

namespace Uraty.Features.Character
{
    public sealed class BarBase : MonoBehaviour
    {
        [SerializeField] private Image _leftFillImage;
        [SerializeField] private Image _rightFillImage;

        [SerializeField] private RectTransform _mainRectTransform;

        [SerializeField] private RectTransform _leftRectTransform;
        [SerializeField] private RectTransform _rightRectTransform;

        private float _halfWidth = 135.0f;
        private float _barRatio = 1.0f;

        public float BarRatio => _barRatio;

        private void Start()
        {
            _halfWidth = _leftRectTransform.rect.width;
        }

        public void SetBarRatio(float ratio)
        {
            float clampedRatio = Mathf.Clamp01(ratio);

            _leftFillImage.fillAmount = clampedRatio;
            _rightFillImage.fillAmount = clampedRatio;

            Vector2 leftPivot = _leftRectTransform.pivot;
            leftPivot.x = clampedRatio;
            _leftRectTransform.pivot = leftPivot;

            Vector2 rightPivot = _rightRectTransform.pivot;
            rightPivot.x = 1.0f - clampedRatio;
            _rightRectTransform.pivot = rightPivot;

            _rightRectTransform.localPosition = _leftRectTransform.localPosition;

            _barRatio = clampedRatio;

            float lostRatio = 1.0f - clampedRatio;
            float offset = _halfWidth * lostRatio;

            _mainRectTransform.localPosition = new Vector3(
                -offset,
                _mainRectTransform.localPosition.y,
                _mainRectTransform.localPosition.z
            );
        }
    }
}
