using UnityEngine;
using UnityEngine.SceneManagement;

using Uraty.Feature.Button;

namespace Uraty.Application.Matching
{
    public sealed class MatchingCancel : MonoBehaviour
    {
        [Header("入力管理")]
        [SerializeField] private ButtonSystem _buttonSystem;

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

            if (_buttonSystem == null)
            {
                Debug.LogError($"{nameof(MatchingCancel)}: GameInputが設定されていません。");
                return;
            }

            _buttonSystem.AddPressedListener(LoadTargetScene);
        }

        private void OnDisable()
        {
            if (_buttonSystem == null)
            {
                return;
            }

            _buttonSystem.RemovePressedListener(LoadTargetScene);
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
