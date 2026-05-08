using UnityEngine;
using UnityEngine.SceneManagement;

using Uraty.Feature.Button;

namespace Uraty.Application.Matching
{
    public sealed class MatchingResultController : MonoBehaviour
    {
        [Header("リトライボタン")]
        [SerializeField]
        private ButtonSystem _retryButton;

        [Header("シーン遷移先")]
        [SerializeField] private string _retrySceneName = "MatchingScene";

        [Header("ロビーボタン")]
        [SerializeField]　private ButtonSystem _lobbyButton;

        [Header("シーン遷移先")]
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        private bool _hasLoadedScene;

        private void OnEnable()
        {
            _hasLoadedScene = false;

            if (_retryButton != null)
            {
                _retryButton.AddPressedListener(HandleRetryButtonPressed);
            }
            else
            {
                Debug.LogError($"{nameof(MatchingResultController)}: リトライボタンが設定されていません。");
            }

            if (_lobbyButton != null)
            {
                _lobbyButton.AddPressedListener(HandleLobbyButtonPressed);
            }
            else
            {
                Debug.LogError($"{nameof(MatchingResultController)}: ロビーボタンが設定されていません。");
            }
        }

        private void OnDisable()
        {
            if (_retryButton != null)
            {
                _retryButton.RemovePressedListener(HandleRetryButtonPressed);
            }

            if (_lobbyButton != null)
            {
                _lobbyButton.RemovePressedListener(HandleLobbyButtonPressed);
            }
        }

        private void HandleRetryButtonPressed()
        {
            LoadScene(_retrySceneName);
        }

        private void HandleLobbyButtonPressed()
        {
            LoadScene(_lobbySceneName);
        }

        private void LoadScene(string sceneName)
        {
            if (_hasLoadedScene)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"{nameof(MatchingResultController)}: 遷移先Scene名が設定されていません。");
                return;
            }

            _hasLoadedScene = true;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
