using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Uraty.Application.Title
{
    public sealed class LoadingManager : MonoBehaviour
    {
        [SerializeField] private Slider _loadingBar;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private const string NextSceneName = "LobbyScene";
        [SerializeField] private const float MinLoadingSeconds = 4.0f;

        private void Start()
        {
            if (_loadingBar != null)
            {
                _loadingBar.value = 0f;
            }
            if (_progressText != null)
            {
                _progressText.text = "0%";
            }

            StartCoroutine(LoadProcessRoutine());
        }

        private IEnumerator LoadProcessRoutine()
        {
            System.Type storeType = System.Type.GetType("Uraty.Shared.Setting.GameSettingsStore, Uraty.Shared.Setting");
            if (storeType != null)
            {
                var loadMethod = storeType.GetMethod("Load", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (loadMethod != null)
                {
                    loadMethod.Invoke(null, null);
                }
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(NextSceneName);

            // true にすると即座に遷移してしまうため、完了まで手動で制御する
            asyncLoad.allowSceneActivation = false;

            float elapsed = 0f;

            // ランダムなロード演出用の変数
            float currentFakeProgress = 0f;
            float targetFakeProgress = 0f;
            float stateTimer = 0f;
            bool isPaused = true; // trueにしておくことで、最初のフレームで必ず「進む」状態からスタートする
            float burstSpeed = 1f;

            while (true)
            {
                elapsed += Time.deltaTime;

                // ランダムに止まったり進んだりする演出の計算
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    if (isPaused)
                    {
                        // 停止状態から進む状態へ
                        isPaused = false;
                        stateTimer = Random.Range(0.2f, 0.8f); // 進む時間
                        
                        float remaining = 1f - targetFakeProgress;
                        targetFakeProgress += Random.Range(remaining * 0.2f, remaining * 0.7f);
                        burstSpeed = (targetFakeProgress - currentFakeProgress) / stateTimer;
                    }
                    else
                    {
                        // 進む状態から停止状態へ
                        isPaused = true;
                        stateTimer = Random.Range(0.1f, 0.6f); // 止まる時間
                    }
                }

                if (!isPaused)
                {
                    currentFakeProgress = Mathf.MoveTowards(currentFakeProgress, targetFakeProgress, burstSpeed * Time.deltaTime);
                }

                // 最低表示時間を超えたら強制的に100%を目指す
                if (elapsed >= MinLoadingSeconds)
                {
                    currentFakeProgress = 1f;
                }

                // Unity の仕様で allowSceneActivation = false の間、progress は 0.9f で止まる
                // そのため 0.9f で割って 0～1 に正規化する
                float loadProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

                // ロードとランダムな進行度のうち遅い方に合わせてバーを進める
                float displayProgress = Mathf.Min(loadProgress, currentFakeProgress);

                if (_loadingBar != null)
                {
                    _loadingBar.value = displayProgress;
                }
                
                if (_progressText != null)
                {
                    _progressText.text = $"{Mathf.FloorToInt(displayProgress * 100)}%";
                }

                bool loadDone = asyncLoad.progress >= 0.9f;
                bool timeDone = elapsed >= MinLoadingSeconds;

                if (loadDone && timeDone)
                {
                    if (_loadingBar != null)
                    {
                        _loadingBar.value = 1f;
                    }
                    if (_progressText != null)
                    {
                        _progressText.text = "100%";
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
