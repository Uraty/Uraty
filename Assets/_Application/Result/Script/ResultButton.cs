using UnityEngine;
using UnityEngine.SceneManagement;

namespace Uraty.Features.Result
{
    public class ResultButton : MonoBehaviour
    {
        [Header("シーン名設定")]
        [SerializeField] private string _lobbySceneName;

        [SerializeField] private string _rematchSceneName;

        public void LoadLobbyScene()
        {
            SceneManager.LoadScene(_lobbySceneName);
        }

        public void LoadRematchScene()
        {
            SceneManager.LoadScene(_rematchSceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
