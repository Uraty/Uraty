using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Uraty.Application.Title
{
    public sealed class LoadingManager : MonoBehaviour
    {
        [SerializeField] private Slider _loadingBar;
        [SerializeField] private string _nextSceneName = "LobbyScene";
        [SerializeField] private float _minLoadingSeconds = 4.0f;

        private const string BgmVolumeKey = "BGM_Volume";
        private const string SeVolumeKey  = "SE_Volume";

        private void Start()
        {
            if (_loadingBar != null)
            {
                _loadingBar.value = 0f;
            }

            StartCoroutine(LoadProcessRoutine());
        }

        private IEnumerator LoadProcessRoutine()
        {
            float bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 1.0f);
            float seVolume  = PlayerPrefs.GetFloat(SeVolumeKey,  1.0f);

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(_nextSceneName);

            // true にすると即座に遷移してしまうため、完了まで手動で制御する
            asyncLoad.allowSceneActivation = false;

            float elapsed = 0f;

            while (true)
            {
                elapsed += Time.deltaTime;

                // Unity の仕様で allowSceneActivation = false の間、progress は 0.9f で止まる
                // そのため 0.9f で割って 0～1 に正規化する
                float loadProgress    = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                float timeProgress    = Mathf.Clamp01(elapsed / _minLoadingSeconds);

                // ロードと最低表示時間のうち遅い方に合わせてバーを進める
                float displayProgress = Mathf.Min(loadProgress, timeProgress);

                if (_loadingBar != null)
                {
                    _loadingBar.value = displayProgress;
                }

                bool loadDone = asyncLoad.progress >= 0.9f;
                bool timeDone = elapsed >= _minLoadingSeconds;

                if (loadDone && timeDone)
                {
                    if (_loadingBar != null)
                    {
                        _loadingBar.value = 1f;
                    }

                    // バーが 100% になった状態を 1 フレーム表示してから遷移する
                    yield return null;

                    asyncLoad.allowSceneActivation = true;
                    yield break;
                }

                yield return null;
            }
        }
    }
}
