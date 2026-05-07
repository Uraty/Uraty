using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

using Uraty.Systems.Input;

namespace Uraty.Application.Matching
{
    public sealed class MatchingCancel : MonoBehaviour
    {
        [Header("入力管理")]
        [SerializeField] private GameInput _gameInput;

        [Header("クリック判定対象Image")]
        [SerializeField] private RectTransform _cancelImageRectTransform;

        [Header("対象Imageが所属するCanvas")]
        [SerializeField] private Canvas _targetCanvas;

        [Header("遷移先Scene名")]
        [SerializeField] private string _targetSceneName = "";

        [Header("遷移方式")]
        [SerializeField] private LoadSceneMode _loadSceneMode = LoadSceneMode.Single;

        [Header("GameInput.Cancelで遷移するか")]
        [SerializeField] private bool isUseCancelInput = false;

        private bool _hasLoadedScene;

        private void OnEnable()
        {
            _hasLoadedScene = false;

            if (_gameInput == null)
            {
                Debug.LogError($"{nameof(MatchingCancel)}: GameInputが設定されていません。");
                return;
            }

            _gameInput.EnableUIInput();

            // 右クリック / ゲームパッドBなどのCancel入力
            if(isUseCancelInput)
            {
                _gameInput.UI.Cancel.performed += HandleCancelPerformed;
            }
            _gameInput.UI.Submit.performed += HandleSubmitPerformed;
        }

        private void OnDisable()
        {
            if (_gameInput == null)
            {
                return;
            }
            if (isUseCancelInput)
            {
                _gameInput.UI.Cancel.performed -= HandleCancelPerformed;
            }
            _gameInput.UI.Submit.performed -= HandleSubmitPerformed;
        }

        /// <summary>
        /// UIのSubmit入力が行われた時、対象Image上ならキャンセル処理へ流す。
        /// Submit は「決定・実行」入力を表す。
        /// </summary>
        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            Vector2 pointerPosition = _gameInput.UI.Point.ReadValue<Vector2>();
            Debug.Log($"{nameof(MatchingCancel)}: Submit入力検知 pointerPosition = {pointerPosition}");

            if (!IsPointerOverCancelImage())
            {
                Debug.Log($"{nameof(MatchingCancel)}: 対象Image外です。");
                return;
            }

            Debug.Log($"{nameof(MatchingCancel)}: 対象Image上でSubmit入力を検知しました。");
            HandleCancelPerformed(context);
        }

        /// <summary>
        /// UIのCancel入力が行われた時に、指定したSceneへ遷移する。
        /// Cancel は「取り消し」や「戻る」操作を表す。
        /// </summary>
        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            LoadTargetScene();
        }

        /// <summary>
        /// 現在のポインター位置がキャンセル用Imageの範囲内か確認する。
        /// </summary>
        private bool IsPointerOverCancelImage()
        {
            if (_cancelImageRectTransform == null)
            {
                Debug.LogError($"{nameof(MatchingCancel)}: クリック判定対象Imageが設定されていません。");
                return false;
            }

            if (Mouse.current == null)
            {
                Debug.LogError($"{nameof(MatchingCancel)}: Mouseが取得できません。");
                return false;
            }

            Vector2 pointerPosition = Mouse.current.position.ReadValue();

            Camera targetCamera = null;

            if (_targetCanvas != null && _targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                targetCamera = _targetCanvas.worldCamera;
            }

            bool isPointerOverImage = RectTransformUtility.RectangleContainsScreenPoint(
                _cancelImageRectTransform,
                pointerPosition,
                targetCamera);

            Debug.Log(
                $"{nameof(MatchingCancel)}: pointerPosition = {pointerPosition}, isPointerOverImage = {isPointerOverImage}");

            return isPointerOverImage;
        }

        private void LoadTargetScene()
        {
            if (_hasLoadedScene)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_targetSceneName))
            {
                Debug.LogError($"{nameof(MatchingCancel)}: 遷移先Scene名が設定されていません。");
                return;
            }

            _hasLoadedScene = true;
            SceneManager.LoadScene(_targetSceneName, _loadSceneMode);
        }
    }
}
