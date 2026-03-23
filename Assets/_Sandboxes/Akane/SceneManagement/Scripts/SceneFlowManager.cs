using System;
using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Uraty.Feature.SceneManagement
{
    // シーンの切り替えを管理するクラス。シーンの読み込みとアンロードを行う。
    public sealed class SceneFlowManager : MonoBehaviour
    {
        [SerializeField] private SceneCatalog catalog;
        [SerializeField] private SceneId firstScene = SceneId.Title;    // 最初に読み込むシーンID

        public SceneId CurrentSceneId
        {
            get; private set;
        }
        public bool IsLoading
        {
            get; private set;
        }

        // シーンが切り替わったときに呼び出されるイベント
        // 引数は切り替え後のシーンID
        public event Action<SceneId> SceneChanged;

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

            // すでに読み込まれているシーンと同じIDがリクエストされた場合は無視する
            StartCoroutine(ChangeSceneRoutine(nextId));
        }

        private IEnumerator ChangeSceneRoutine(SceneId nextId)
        {
            IsLoading = true;

            string nextPath = catalog.GetPath(nextId);

            // 先に次を読む
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(nextPath, LoadSceneMode.Additive);
            while (!loadOp.isDone)
            {
                yield return null;
            }

            // 読み込んだシーンをアクティブにする
            Scene nextScene = SceneManager.GetSceneByPath(nextPath);
            if (nextScene.IsValid())
            {
                SceneManager.SetActiveScene(nextScene);
            }

            // 前のコンテンツシーンを外す
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                // アンロードは非同期で行い、完了するまで待つ
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentScenePath);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                    {
                        yield return null;
                    }
                }
            }

            currentScenePath = nextPath;
            CurrentSceneId = nextId;
            IsLoading = false;

            // シーンの切り替えが完了したことを通知する
            SceneChanged?.Invoke(nextId);
        }
    }
}
