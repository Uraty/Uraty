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

        [Header("押された時に実行する処理")]
        [SerializeField] private UnityEvent _pressed = new UnityEvent();

        private bool _isSubscribed;

        private void Awake()
        {
            if (_targetRectTransform == null)
            {
                _targetRectTransform = GetComponent<RectTransform>();
            }
        }

        private void OnEnable()
        {
            if (_gameInput == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: GameInputが設定されていません。");
                return;
            }

            _gameInput.EnableUIInput();
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
            _pressed.AddListener(listener);
        }

        /// <summary>
        /// 登録済みの関数を解除する。
        /// </summary>
        public void RemovePressedListener(UnityAction listener)
        {
            _pressed.RemoveListener(listener);
        }

        public void UseSubmit()
        {
            _usesSubmit = true;
            _usesCancel = false;
        }

        public void UseCancel()
        {
            _usesSubmit = false;
            _usesCancel = true;
        }

        public void UseSubmitCancel()
        {
            _usesSubmit = true;
            _usesCancel = true;
        }

        public void UseNone()
        {
            _usesSubmit = false;
            _usesCancel = false;
        }

        private void SubscribeInput()
        {
            if (_isSubscribed)
            {
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

            _isSubscribed = true;
        }

        private void UnsubscribeInput()
        {
            if (!_isSubscribed || _gameInput == null)
            {
                return;
            }

            if (_usesSubmit)
            {
                _gameInput.UI.Submit.performed -= HandleSubmitPerformed;
            }

            if (_usesCancel)
            {
                _gameInput.UI.Cancel.performed -= HandleCancelPerformed;
            }

            _isSubscribed = false;
        }

        /// <summary>
        /// Submit入力が行われた時に、ボタン押下処理へ流す。
        /// Submit は「決定・実行」入力を表す。
        /// </summary>
        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            PressIfAllowed();
        }

        /// <summary>
        /// Cancel入力が行われた時に、ボタン押下処理へ流す。
        /// Cancel は「取り消し・戻る」入力を表す。
        /// </summary>
        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            PressIfAllowed();
        }

        private void PressIfAllowed()
        {
            if (_requiresPointerInside && !IsPointerInsideTarget())
            {
                return;
            }

            Debug.Log($"{nameof(ButtonSystem)}: ボタンが押されました。");
            _pressed.Invoke();
        }

        private bool IsPointerInsideTarget()
        {
            if (_targetRectTransform == null)
            {
                Debug.LogError($"{nameof(ButtonSystem)}: クリック判定対象が設定されていません。");
                return false;
            }

            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 pointerPosition = Mouse.current.position.ReadValue();

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
