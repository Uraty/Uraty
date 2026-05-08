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

        [SerializeField, Tooltip("押された時に実行する処理")]
        private UnityEvent _pressed = new UnityEvent();

        private bool _isInputSubscribed;

        private void Awake()
        {
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
        }

        private void OnEnable()
        {
            LogDebug("OnEnableが呼ばれました。");

            if (_gameInput == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: Scene内にGameInputが見つかりません。");
                return;
            }

            LogDebug("GameInput.EnableUIInput() を呼び出しました。");

            if (!_gameInput.UI.enabled)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: GameInput.UIが有効化されていません。");
                return;
            }

            LogDebug("GameInput.UI は有効です。");

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
            LogDebug("OnDisableが呼ばれました。");
            UnsubscribeInput();
        }

        /// <summary>
        /// ボタンが押された時に実行する関数を登録する。
        /// </summary>
        public void AddPressedListener(UnityAction listener)
        {
            if (listener == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: 登録しようとした関数がnullです。");
                return;
            }

            _pressed.AddListener(listener);
            LogDebug("PressedListenerを登録しました。");
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
            LogDebug("PressedListenerを解除しました。");
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
            LogDebug($"入力モード変更開始: Submit={usesSubmit}, Cancel={usesCancel}");

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

            LogDebug($"入力モード変更完了: Submit={_usesSubmit}, Cancel={_usesCancel}");
        }

        private void SubscribeInput()
        {
            if (_isInputSubscribed)
            {
                LogDebug("すでに入力購読済みのため、SubscribeInputをスキップしました。");
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
                LogDebug("UI.Submit.performed を購読しました。");
            }

            if (_usesCancel)
            {
                _gameInput.UI.Cancel.performed += HandleCancelPerformed;
                LogDebug("UI.Cancel.performed を購読しました。");
            }

            _isInputSubscribed = _usesSubmit || _usesCancel;
            LogDebug($"SubscribeInput完了: IsInputSubscribed={_isInputSubscribed}");
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
            LogDebug("Submit / Cancel の購読を解除しました。");
        }

        /// <summary>
        /// Submit入力が行われた時に、ボタン押下処理へ流す。
        /// Submit は「決定・実行」入力を表す。
        /// </summary>
        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            LogDebug(
                $"Submit入力を検知しました。Device={context.control.device.displayName}, Control={context.control.name}");

            InvokePressedIfAllowed(context);
        }

        /// <summary>
        /// Cancel入力が行われた時に、ボタン押下処理へ流す。
        /// Cancel は「取り消し・戻る」入力を表す。
        /// </summary>
        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            LogDebug(
                $"Cancel入力を検知しました。Device={context.control.device.displayName}, Control={context.control.name}");

            InvokePressedIfAllowed(context);
        }

        private void InvokePressedIfAllowed(InputAction.CallbackContext context)
        {
            bool hasPointerPosition = TryGetPointerPosition(context, out Vector2 pointerPosition);

            LogDebug(
                $"押下判定開始: RequiresPointerInside={_requiresPointerInside}, HasPointerPosition={hasPointerPosition}, AllowsNonPointerInput={_allowsNonPointerInput}, PointerPosition={pointerPosition}");

            if (_requiresPointerInside && hasPointerPosition && !IsPointerInsideTarget(pointerPosition))
            {
                LogDebug("対象UI外で押されたため、Pressedを実行しません。");
                return;
            }

            if (_requiresPointerInside && !hasPointerPosition && !_allowsNonPointerInput)
            {
                LogDebug("非ポインター入力が許可されていないため、Pressedを実行しません。");
                return;
            }

            LogDebug("Pressedを実行します。");
            _pressed.Invoke();
        }

        /// <summary>
        /// 入力を発生させたデバイスから、ポインター座標を取得する。
        /// Mouse は「マウス」、Pen は「ペン」、Touchscreen は「タッチ画面」を表す。
        /// </summary>
        private bool TryGetPointerPosition(
            InputAction.CallbackContext context,
            out Vector2 pointerPosition)
        {
            pointerPosition = Vector2.zero;

            if (context.control.device is Mouse mouse)
            {
                pointerPosition = mouse.position.ReadValue();
                LogDebug($"Mouse座標を取得しました: {pointerPosition}");
                return true;
            }

            if (context.control.device is Pen pen)
            {
                pointerPosition = pen.position.ReadValue();
                LogDebug($"Pen座標を取得しました: {pointerPosition}");
                return true;
            }

            if (context.control.device is Touchscreen)
            {
                Touchscreen touchscreen = Touchscreen.current;

                if (touchscreen == null)
                {
                    LogDebug("Touchscreen.current がnullです。");
                    return false;
                }

                pointerPosition = touchscreen.primaryTouch.position.ReadValue();
                LogDebug($"Touchscreen座標を取得しました: {pointerPosition}");
                return true;
            }

            LogDebug($"ポインター座標を持たない入力です: Device={context.control.device.displayName}");
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

            bool isPointerInside = RectTransformUtility.RectangleContainsScreenPoint(
                _targetRectTransform,
                pointerPosition,
                targetCamera);

            LogDebug($"PointerPosition={pointerPosition}, IsPointerInside={isPointerInside}");

            return isPointerInside;
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
