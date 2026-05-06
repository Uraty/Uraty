using UnityEngine;
using UnityEngine.SceneManagement;

namespace Uraty.Application.Matching
{
    public sealed class MatchingController : MonoBehaviour
    {
        [Header("遷移先Scene名")]
        [SerializeField] private string _targetSceneName = "";

        [Header("遷移方式")]
        [SerializeField] private LoadSceneMode _loadSceneMode = LoadSceneMode.Single;

        [Header("自動遷移までの秒数")]
        [SerializeField] private float _autoTransitionSeconds = 0.0f;

        private float _elapsedSeconds;
        private bool _hasLoadedScene;

        private void OnEnable()
        {
            _elapsedSeconds = 0.0f;
            _hasLoadedScene = false;
        }

        private void Update()
        {
            if (_hasLoadedScene)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;

            if (_elapsedSeconds < _autoTransitionSeconds)
            {
                return;
            }

            LoadTargetScene();
        }

        private void LoadTargetScene()
        {
            if (string.IsNullOrWhiteSpace(_targetSceneName))
            {
                Debug.LogError($"{nameof(MatchingController)}: 遷移先Scene名が設定されていません。");
                return;
            }

            _hasLoadedScene = true;
            SceneManager.LoadScene(_targetSceneName, _loadSceneMode);
        }

        private void OnValidate()
        {
            if (_autoTransitionSeconds < 0.0f)
            {
                _autoTransitionSeconds = 0.0f;
            }
        }
    }
}
