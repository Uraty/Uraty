using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// 決定後だけ十字キー左右で値を変更できるSlider。
    /// </summary>
    public sealed class ConfirmableSlider : Slider, ISubmitHandler, ICancelHandler
    {
        [Header("Pad Edit")]
        [SerializeField] private GameObject _editingMarker;

        private const float _stepRatio = 0.01f;
        private const float MinimumStepValue = 0.0001f;

        private bool _isEditing;
        public bool IsEditing => _isEditing;

        public override void OnMove(AxisEventData eventData)
        {
            if (_isEditing)
            {
                HandleEditingMove(eventData);
                return;
            }

            if (eventData.moveDir == MoveDirection.Left ||
                eventData.moveDir == MoveDirection.Right)
            {
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            _isEditing = !_isEditing;
            RefreshEditingMarker();

            eventData.Use();
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (!_isEditing)
            {
                return;
            }

            _isEditing = false;
            RefreshEditingMarker();

            eventData.Use();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            _isEditing = false;
            RefreshEditingMarker();

            base.OnDeselect(eventData);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshEditingMarker();
        }

        private void HandleEditingMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
            {
                AddValue(-GetStepValue());
                eventData.Use();
                return;
            }

            if (eventData.moveDir == MoveDirection.Right)
            {
                AddValue(GetStepValue());
                eventData.Use();
                return;
            }

            eventData.Use();
        }

        private void AddValue(float amount)
        {
            if (IsReverseDirection())
            {
                amount *= -1f;
            }

            value = Mathf.Clamp(value + amount, minValue, maxValue);
        }

        private float GetStepValue()
        {
            if (wholeNumbers)
            {
                return 1f;
            }

            float range = maxValue - minValue;

            if (range <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(range * _stepRatio, MinimumStepValue);
        }

        private bool IsReverseDirection()
        {
            return direction == Direction.RightToLeft ||
                   direction == Direction.TopToBottom;
        }

        private void RefreshEditingMarker()
        {
            if (_editingMarker == null)
            {
                return;
            }

            _editingMarker.SetActive(_isEditing);
        }
    }
}
