using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Uraty.Features.Button
{
    /// <summary>
    /// PadCursor自身にアタッチし、UI/Pointでカーソルを動かしてButtonScalingを判定する。
    /// EventSystem / GraphicRaycaster は使用しない。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ButtonPadCursorController : MonoBehaviour
    {
        private const string UiActionMapName = "UI";
        private const string PointActionName = "Point";
        private const string SubmitActionName = "Submit";

        private const float StickInputMaxMagnitude = 1.1f;

        [SerializeField, Tooltip("GameInputActions.inputactions")]
        private InputActionAsset _gameInputActions;

        [SerializeField, Tooltip("UICanvasのRectTransform")]
        private RectTransform _canvasRectTransform;

        [SerializeField, Tooltip("PadCursorの見た目。未設定でも問題なし")]
        private Graphic _cursorGraphic;

        [SerializeField, Tooltip("PadCursorの移動速度")]
        private float _cursorSpeed = 900.0f;

        [SerializeField, Tooltip("Point入力時にPadCursorの見た目を表示する")]
        private bool _activateCursorOnPointInput = true;

        [SerializeField, Tooltip("InactiveなButtonも検索対象に含め、使用時にActive判定で除外する")]
        private bool _includeInactiveButtonsOnSearch = true;

        private readonly List<ButtonScaling> _targetButtons = new();

        private RectTransform _padCursorRectTransform;
        private InputAction _pointAction;
        private InputAction _submitAction;
        private ButtonScaling _currentHoverButton;

        private void Awake()
        {
            _padCursorRectTransform = GetComponent<RectTransform>();

            if (_cursorGraphic == null)
            {
                _cursorGraphic = GetComponent<Graphic>();
            }

            InputActionMap uiActionMap = _gameInputActions.FindActionMap(UiActionMapName, true);
            _pointAction = uiActionMap.FindAction(PointActionName, true);
            _submitAction = uiActionMap.FindAction(SubmitActionName, true);

            RefreshTargetButtons();
            SetCursorVisible(false);
        }

        private void OnEnable()
        {
            _pointAction.Enable();

            _submitAction.performed += HandleSubmitPerformed;
            _submitAction.Enable();

            RefreshTargetButtons();
        }

        private void OnDisable()
        {
            _submitAction.performed -= HandleSubmitPerformed;
            _submitAction.Disable();

            _pointAction.Disable();

            SetCurrentHoverButton(null);
        }

        private void Update()
        {
            MovePadCursor();
            UpdateHoverScaling();
        }

        /// <summary>
        /// マルチシーンを含む現在ロード済みシーン内のButtonScalingを再取得する。
        /// </summary>
        public void RefreshTargetButtons()
        {
            _targetButtons.Clear();

            FindObjectsInactive inactiveSearchMode = _includeInactiveButtonsOnSearch
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;

            ButtonScaling[] foundButtons = FindObjectsByType<ButtonScaling>(
                inactiveSearchMode,
                FindObjectsSortMode.None);

            foreach (ButtonScaling foundButton in foundButtons)
            {
                if (foundButton == null)
                {
                    continue;
                }

                _targetButtons.Add(foundButton);
            }
        }

        private void MovePadCursor()
        {
            Vector2 pointValue = _pointAction.ReadValue<Vector2>();

            if (pointValue == Vector2.zero)
            {
                return;
            }

            ActivatePadCursor();

            if (pointValue.magnitude <= StickInputMaxMagnitude)
            {
                Vector2 moveAmount = pointValue * (_cursorSpeed * Time.unscaledDeltaTime);
                _padCursorRectTransform.anchoredPosition += moveAmount;
                ClampPadCursorToCanvas();
                return;
            }

            if (_canvasRectTransform == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRectTransform,
                pointValue,
                null,
                out Vector2 localPoint);

            _padCursorRectTransform.anchoredPosition = localPoint;
            ClampPadCursorToCanvas();
        }

        private void UpdateHoverScaling()
        {
            ButtonScaling hoverButton = FindHoverButton();
            SetCurrentHoverButton(hoverButton);
        }

        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            ActivatePadCursor();

            ButtonScaling hoverButton = FindHoverButton();

            if (hoverButton == null)
            {
                return;
            }

            hoverButton.PlayPressedByPadCursor();
        }

        private ButtonScaling FindHoverButton()
        {
            ButtonScaling resultButton = null;
            int resultOrder = int.MinValue;

            foreach (ButtonScaling targetButton in _targetButtons)
            {
                if (!CanUseButton(targetButton))
                {
                    continue;
                }

                if (!IsPadCursorInsideButton(targetButton.HitboxRectTransform))
                {
                    continue;
                }

                int order = GetUiOrder(targetButton.HitboxRectTransform);

                if (resultButton != null && order < resultOrder)
                {
                    continue;
                }

                resultButton = targetButton;
                resultOrder = order;
            }

            return resultButton;
        }

        private bool CanUseButton(ButtonScaling targetButton)
        {
            if (targetButton == null)
            {
                return false;
            }

            if (!targetButton.gameObject.scene.isLoaded)
            {
                return false;
            }

            if (!targetButton.gameObject.activeInHierarchy)
            {
                return false;
            }

            RectTransform hitboxRectTransform = targetButton.HitboxRectTransform;

            if (hitboxRectTransform == null)
            {
                return false;
            }

            if (!hitboxRectTransform.gameObject.activeInHierarchy)
            {
                return false;
            }

            return true;
        }

        private bool IsPadCursorInsideButton(RectTransform buttonRectTransform)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(
                buttonRectTransform,
                _padCursorRectTransform.position,
                null);
        }

        private void SetCurrentHoverButton(ButtonScaling nextHoverButton)
        {
            if (_currentHoverButton == nextHoverButton)
            {
                return;
            }

            if (_currentHoverButton != null)
            {
                _currentHoverButton.SetPadCursorInside(false);
            }

            _currentHoverButton = nextHoverButton;

            if (_currentHoverButton != null)
            {
                _currentHoverButton.SetPadCursorInside(true);
            }
        }

        private void ActivatePadCursor()
        {
            if (!_activateCursorOnPointInput)
            {
                return;
            }

            SetCursorVisible(true);
        }

        private void SetCursorVisible(bool isVisible)
        {
            if (_cursorGraphic == null)
            {
                return;
            }

            _cursorGraphic.enabled = isVisible;
        }

        private void ClampPadCursorToCanvas()
        {
            if (_canvasRectTransform == null)
            {
                return;
            }

            Vector2 position = _padCursorRectTransform.anchoredPosition;
            Rect canvasRect = _canvasRectTransform.rect;

            position.x = Mathf.Clamp(position.x, canvasRect.xMin, canvasRect.xMax);
            position.y = Mathf.Clamp(position.y, canvasRect.yMin, canvasRect.yMax);

            _padCursorRectTransform.anchoredPosition = position;
        }

        private static int GetUiOrder(RectTransform rectTransform)
        {
            int order = 0;
            Transform currentTransform = rectTransform;

            while (currentTransform != null)
            {
                order += currentTransform.GetSiblingIndex();
                currentTransform = currentTransform.parent;
            }

            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                order += canvas.sortingOrder * 10000;
            }

            return order;
        }
    }
}
