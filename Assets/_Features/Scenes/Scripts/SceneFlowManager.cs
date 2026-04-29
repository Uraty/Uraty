using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using R3;

namespace Uraty.Features.Scenes
{
    // シーンの切り替えを管理するクラス。シーンの読み込みとアンロードを行う。
    public sealed class SceneFlowManager : MonoBehaviour
    {
        [SerializeField] private SceneCatalog catalog;
        [SerializeField] private SceneId firstScene = SceneId.Title;

        public SceneId CurrentSceneId
        {
            get; private set;
        }
        public bool IsLoading
        {
            get; private set;
        }

        // =========================
        // Stream
        // =========================

        private readonly Subject<SceneId> _sceneChangedSubject = new();

        public Observable<SceneId> SceneChangedStream => _sceneChangedSubject;

        // =========================

        private string currentScenePath;

        private IEnumerator Start()
        {
            yield return ChangeSceneRoutine(firstScene);
        }

        public void RequestChange(SceneId nextId)
        {
            if (IsLoading)
            {
                return;
            }

            string nextPath = catalog.GetPath(nextId);
            if (currentScenePath == nextPath)
            {
                return;
            }

            StartCoroutine(ChangeSceneRoutine(nextId));
        }

        private IEnumerator ChangeSceneRoutine(SceneId nextId)
        {
            IsLoading = true;

            string nextPath = catalog.GetPath(nextId);

            // =========================
            // Load
            // =========================

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(nextPath, LoadSceneMode.Additive);
            while (!loadOp.isDone)
            {
                yield return null;
            }

            // =========================
            // Set Active
            // =========================

            Scene nextScene = SceneManager.GetSceneByPath(nextPath);
            if (nextScene.IsValid())
            {
                SceneManager.SetActiveScene(nextScene);
            }

            // =========================
            // Unload previous
            // =========================

            if (!string.IsNullOrEmpty(currentScenePath))
            {
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentScenePath);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                    {
                        yield return null;
                    }
                }
            }

            // =========================
            // State update
            // =========================

            currentScenePath = nextPath;
            CurrentSceneId = nextId;
            IsLoading = false;

            // =========================
            // Notify
            // =========================

            PublishSceneChanged(nextId);
        }

        private void PublishSceneChanged(SceneId sceneId)
        {
            _sceneChangedSubject.OnNext(sceneId);
        }

        private void OnDestroy()
        {
            _sceneChangedSubject.Dispose();
        }
    }
}
