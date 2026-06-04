using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

using Uraty.Systems.Input;

namespace Uraty.Features.Button
{
    /// <summary>
    /// EventSystemを使わず、GameInputだけでUIボタンの押下判定を行う。
    /// </summary>
    public sealed class ButtonSystem : MonoBehaviour
    {
        [SerializeField, Tooltip("入力管理")]
        private GameInput _gameInput;

        [SerializeField, Tooltip("クリック判定対象")]
        private RectTransform _targetRectTransform;

        [SerializeField, Tooltip("対象UIが所属するCanvas")]
        private Canvas _targetCanvas;

        [SerializeField, Tooltip("Submit入力で押下判定を行う")]
        private bool _usesSubmit = true;

        [SerializeField, Tooltip("Cancel入力で押下判定を行う")]
        private bool _usesCancel = false;

        [SerializeField, Tooltip("対象UI上で押された時だけ反応する")]
        private bool _requiresPointerInside = true;

        [SerializeField, Tooltip("ゲームパッドやキーボードなど、座標を持たない入力も許可する")]
        private bool _allowsNonPointerInput = false;

        [SerializeField, Tooltip("挙動確認用ログを出力する")]
        private bool _outputsDebugLog = true;

        [SerializeField, Tooltip("押下要求後に完了待ちする処理数。Scaling + SEなら2")]
        private int _requiredCompleteCount = 2;

        [SerializeField, Tooltip("押された時に実行する処理")]
        private UnityEvent _pressed = new UnityEvent();

        private readonly UnityEvent _pressedRequested = new UnityEvent();

        private bool _isInputSubscribed;
        private bool _isPointerInside;
        private bool _wasPressed;

        private bool _isWaitingComplete;
        private int _completedCount;

        public bool IsPressed => _wasPressed;
        public bool IsPointerInside => _isPointerInside;

        private void Start()
        {
            Debug.Log($"{nameof(ButtonSystem)} が Start されました。", this);

            if (_gameInput == null)
            {
                _gameInput = FindFirstObjectByType<GameInput>();
                LogDebug("Scene内からGameInputを検索しました。");
            }

            if (_targetRectTransform == null)
            {
                _targetRectTransform = GetComponent<RectTransform>();
                LogDebug("自身のRectTransformをクリック判定対象に設定しました。");
            }

            if (_targetCanvas == null)
            {
                _targetCanvas = GetComponentInParent<Canvas>();
                LogDebug("親階層からCanvasを取得しました。");
            }

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

        private void Update()
        {
            UpdatePointerInsideState();
        }

        private void OnDestroy()
        {
            UnsubscribeInput();
        }

        public void AddPressedListener(UnityAction listener)
        {
            if (listener == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: 登録しようとした関数がnullです。");
                return;
            }

            _pressed.AddListener(listener);
        }

        public void RemovePressedListener(UnityAction listener)
        {
            if (listener == null)
            {
                return;
            }

            _pressed.RemoveListener(listener);
        }

        public void AddPressedRequestedListener(UnityAction requestedListener)
        {
            if (requestedListener == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: 登録しようとした関数がnullです。");
                return;
            }

            _pressedRequested.AddListener(requestedListener);
        }

        public void RemovePressedRequestedListener(UnityAction requestedListener)
        {
            if (requestedListener == null)
            {
                return;
            }

            _pressedRequested.RemoveListener(requestedListener);
        }

        public void NotifyPressedSequenceCompleted()
        {
            if (!_isWaitingComplete)
            {
                return;
            }

            _completedCount++;
            LogDebug($"押下演出完了通知: {_completedCount}/{_requiredCompleteCount}");

            if (_completedCount < _requiredCompleteCount)
            {
                return;
            }

            _isWaitingComplete = false;
            _completedCount = 0;

            InvokePressed();
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

            _gameInput.UI.Submit.performed -= HandleSubmitPerformed;
            _gameInput.UI.Cancel.performed -= HandleCancelPerformed;

            _isInputSubscribed = false;
        }

        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            InvokePressedIfAllowed(context);
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            InvokePressedIfAllowed(context);
        }

        public void InvokePressed()
        {
            LogDebug("登録済みPressedを実行します。");
            _pressed.Invoke();
        }

        public void InvokePressedIfAllowed(InputAction.CallbackContext context)
        {
            if (_isWaitingComplete)
            {
                LogDebug("押下演出待機中のため、入力を無視します。");
                return;
            }

            _wasPressed = false;

            bool hasPointerPosition = TryGetPointerPosition(context, out Vector2 pointerPosition);

            if (_requiresPointerInside && hasPointerPosition && !IsPointerInsideTarget(pointerPosition))
            {
                return;
            }

            if (_requiresPointerInside && !hasPointerPosition && !_allowsNonPointerInput)
            {
                return;
            }

            _wasPressed = true;
            BeginPressedSequence();

            LogDebug("Pressed要求を実行します。");
            _pressedRequested.Invoke();
        }

        private void BeginPressedSequence()
        {
            _completedCount = 0;
            _isWaitingComplete = true;

            if (_requiredCompleteCount <= 0)
            {
                NotifyPressedSequenceCompleted();
            }
        }

        private void UpdatePointerInsideState()
        {
            if (Mouse.current == null)
            {
                _isPointerInside = false;
                return;
            }

            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            _isPointerInside = IsPointerInsideTarget(pointerPosition);
        }

        private bool TryGetPointerPosition(
            InputAction.CallbackContext context,
            out Vector2 pointerPosition)
        {
            pointerPosition = Vector2.zero;

            if (context.control.device is Mouse mouse)
            {
                pointerPosition = mouse.position.ReadValue();
                return true;
            }

            if (context.control.device is Pen pen)
            {
                pointerPosition = pen.position.ReadValue();
                return true;
            }

            if (context.control.device is Touchscreen)
            {
                Touchscreen touchscreen = Touchscreen.current;

                if (touchscreen == null)
                {
                    return false;
                }

                pointerPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            return false;
        }

        private bool IsPointerInsideTarget(Vector2 pointerPosition)
        {
            if (_targetRectTransform == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: クリック判定対象が設定されていません。");
                return false;
            }

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

        private void LogDebug(string message)
        {
            if (!_outputsDebugLog)
            {
                return;
            }

            Debug.Log($"{nameof(ButtonSystem)}: {message}", this);
        }
    }
}
