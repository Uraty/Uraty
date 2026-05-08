using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

using Uraty.Systems.Input;

namespace Uraty.Feature.Button
{
    /// <summary>
    /// EventSystemを使わず、GameInputだけでUIボタンの押下判定を行う。
    /// </summary>
    public sealed class ButtonSystem : MonoBehaviour
    {
        [Header("入力管理")]
        [SerializeField] private GameInput _gameInput;

        [Header("クリック判定対象")]
        [SerializeField] private RectTransform _targetRectTransform;

        [Header("対象UIが所属するCanvas")]
        [SerializeField] private Canvas _targetCanvas;

        [Header("使用する入力")]
        [SerializeField] private bool _usesSubmit = true;

        [SerializeField] private bool _usesCancel = false;

        [Header("対象UI上で押された時だけ反応する")]
        [SerializeField] private bool _requiresPointerInside = true;

        [Header("非ポインター入力を許可する")]
        [SerializeField] private bool _allowsNonPointerInput = false;

        [Header("押された時に実行する処理")]
        [SerializeField] private UnityEvent _pressed = new UnityEvent();

        private bool _isInputSubscribed;

        private void Awake()
        {
            if (_gameInput == null)
            {
                _gameInput = FindFirstObjectByType<GameInput>();
            }

            if (_targetRectTransform == null)
            {
                _targetRectTransform = GetComponent<RectTransform>();
            }

            if (_targetCanvas == null)
            {
                _targetCanvas = GetComponentInParent<Canvas>();
            }
        }

        private void OnEnable()
        {
            if (_gameInput == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: Scene内にGameInputが見つかりません。");
                return;
            }

            if (!_gameInput.UI.enabled)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: GameInput.UIが有効化されていません。");
                return;
            }

            if (_usesSubmit && _gameInput.UI.Submit == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: GameInput.UI.Submitが見つかりません。");
                return;
            }

            if (_usesCancel && _gameInput.UI.Cancel == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: GameInput.UI.Cancelが見つかりません。");
                return;
            }

            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
        }

        /// <summary>
        /// ボタンが押された時に実行する関数を登録する。
        /// </summary>
        public void AddPressedListener(UnityAction listener)
        {
            if (listener == null)
            {
                return;
            }

            _pressed.AddListener(listener);
        }

        /// <summary>
        /// 登録済みの関数を解除する。
        /// </summary>
        public void RemovePressedListener(UnityAction listener)
        {
            if (listener == null)
            {
                return;
            }

            _pressed.RemoveListener(listener);
        }

        public void UseSubmit()
        {
            SetInputMode(usesSubmit: true, usesCancel: false);
        }

        public void UseCancel()
        {
            SetInputMode(usesSubmit: false, usesCancel: true);
        }

        public void UseSubmitCancel()
        {
            SetInputMode(usesSubmit: true, usesCancel: true);
        }

        public void UseNone()
        {
            SetInputMode(usesSubmit: false, usesCancel: false);
        }

        private void SetInputMode(bool usesSubmit, bool usesCancel)
        {
            bool shouldResubscribe = _isInputSubscribed;

            if (shouldResubscribe)
            {
                UnsubscribeInput();
            }

            _usesSubmit = usesSubmit;
            _usesCancel = usesCancel;

            if (shouldResubscribe && isActiveAndEnabled)
            {
                SubscribeInput();
            }
        }

        private void SubscribeInput()
        {
            if (_isInputSubscribed)
            {
                return;
            }

            if (_gameInput == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: GameInputが設定されていません。");
                return;
            }

            if (_usesSubmit)
            {
                _gameInput.UI.Submit.performed += HandleSubmitPerformed;
            }

            if (_usesCancel)
            {
                _gameInput.UI.Cancel.performed += HandleCancelPerformed;
            }

            _isInputSubscribed = _usesSubmit || _usesCancel;
        }

        private void UnsubscribeInput()
        {
            if (_gameInput == null)
            {
                _isInputSubscribed = false;
                return;
            }

            // モードフラグに関係なく、登録される可能性がある入力は必ず解除する。
            _gameInput.UI.Submit.performed -= HandleSubmitPerformed;
            _gameInput.UI.Cancel.performed -= HandleCancelPerformed;

            _isInputSubscribed = false;
        }

        /// <summary>
        /// Submit入力が行われた時に、ボタン押下処理へ流す。
        /// Submit は「決定・実行」入力を表す。
        /// </summary>
        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            InvokePressedIfAllowed(context);
        }

        /// <summary>
        /// Cancel入力が行われた時に、ボタン押下処理へ流す。
        /// Cancel は「取り消し・戻る」入力を表す。
        /// </summary>
        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            InvokePressedIfAllowed(context);
        }

        private void InvokePressedIfAllowed(InputAction.CallbackContext context)
        {
            bool isPointerInput = IsPointerInput(context);

            if (_requiresPointerInside && isPointerInput && !IsPointerInsideTarget())
            {
                return;
            }

            if (_requiresPointerInside && !isPointerInput && !_allowsNonPointerInput)
            {
                return;
            }

            Debug.Log($"{nameof(ButtonSystem)}: ボタンが押されました。");
            _pressed.Invoke();
        }

        private bool IsPointerInput(InputAction.CallbackContext context)
        {
            InputDevice inputDevice = context.control.device;

            return inputDevice is Mouse
                || inputDevice is Touchscreen
                || inputDevice is Pen;
        }

        private bool IsPointerInsideTarget()
        {
            if (_targetRectTransform == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: クリック判定対象が設定されていません。");
                return false;
            }

            Vector2 pointerPosition = _gameInput.UI.Point.ReadValue<Vector2>();

            Camera targetCamera = null;

            if (_targetCanvas != null && _targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                targetCamera = _targetCanvas.worldCamera;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(
                _targetRectTransform,
                pointerPosition,
                targetCamera);
        }
    }
}
