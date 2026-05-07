using UnityEngine;
using UnityEngine.SceneManagement;

namespace Uraty.Features.Result
{
    public class ResultButtan : MonoBehaviour
    {
        [Header("シーン名設定")]
        [SerializeField] private string LobbyName;
        [SerializeField] private string RematchName;

        public void LobbyScene()
        {
            SceneManager.LoadScene(LobbyName);
        }

        public void RematchScene()
        {
            SceneManager.LoadScene(RematchName);
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
