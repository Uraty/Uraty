using System;
using System.Collections;

using R3;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Uraty.Feature.Akane_TestCharacter;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビー画面中央に、現在選択中のキャラPrefabを表示するクラス。
    /// キャラ表示部分を押したら、キャラ選択用Additive Sceneを開く。
    /// </summary>
    public sealed class LobbyCharacterDisplayController : MonoBehaviour
    {
        [Header("View")]
        // キャラPrefabを生成する位置。
        [SerializeField] private Transform _previewRoot;

        // ロビーのメインUI。
        // キャラ選択画面を開いている間は非表示にする。
        [SerializeField] private GameObject _mainPanel;

        // 現在表示中のキャラを押すためのボタン。
        // 透明ボタンとしてキャラ表示部分に重ねる想定。
        [SerializeField] private Button _currentCharacterButton;

        // 現在選択中のキャラ名を表示するText。
        [SerializeField] private TextMeshProUGUI _characterNameText;

        [Header("Scene")]
        // Additiveで読み込むキャラ選択Scene名。
        [SerializeField] private string _characterSelectSceneName = "LobbyCharacterSelectScene";

        [Header("Store")]
        [SerializeField] private CharacterSelectionStore _characterSelectionStore;

        [Header("Default Selection")]
        [SerializeField] private Selectable _returnSelectable;

        // キャラPrefabの表示スケール。必要に応じて調整する。
        [Header("Preview")]
        [SerializeField] private Vector3 _previewScale = Vector3.one;

        private IDisposable _selectedCharacterChangedSubscription;

        // 現在ロビーに表示しているキャラPrefabの実体。
        private GameObject _currentPreviewObject;

        // キャラ選択Sceneの多重読み込みを防ぐためのフラグ。
        private bool _isLoading;

        private void OnEnable()
        {
            if (_currentCharacterButton != null)
            {
                _currentCharacterButton.onClick.AddListener(OpenCharacterSelectScene);
            }

            SceneManager.sceneUnloaded += HandleSceneUnloaded;

            if (_characterSelectionStore != null)
            {
                _selectedCharacterChangedSubscription = _characterSelectionStore
                    .SelectedCharacterPrefabChangedStream
                    .Subscribe(RefreshCharacter);
            }
        }

        private void Start()
        {
            if (_characterSelectionStore == null)
            {
                Debug.LogError("CharacterSelectionStore が設定されていません。");
                return;
            }

            RefreshCharacter(_characterSelectionStore.SelectedCharacterPrefab);
        }

        private void OnDisable()
        {
            if (_currentCharacterButton != null)
            {
                _currentCharacterButton.onClick.RemoveListener(OpenCharacterSelectScene);
            }

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;

            _selectedCharacterChangedSubscription?.Dispose();
            _selectedCharacterChangedSubscription = null;
        }

        /// <summary>
        /// キャラ選択SceneをAdditiveで開く。
        /// </summary>
        private void OpenCharacterSelectScene()
        {
            if (_isLoading)
            {
                return;
            }

            // すでに読み込み済みなら二重に開かない。
            Scene scene = SceneManager.GetSceneByName(_characterSelectSceneName);

            if (scene.isLoaded)
            {
                return;
            }

            StartCoroutine(OpenCharacterSelectSceneRoutine());
        }

        /// <summary>
        /// Additive Scene読み込み処理。
        /// LobbySceneは残したまま、キャラ選択Sceneを追加で読み込む。
        /// </summary>
        private IEnumerator OpenCharacterSelectSceneRoutine()
        {
            _isLoading = true;

            ClearUiSelection();

            // キャラ選択画面を開いている間は、
            // ロビー中央の現在選択中キャラとメインUIを非表示にする。
            SetPreviewRootVisible(false);
            SetMainPanelVisible(false);

            yield return SceneManager.LoadSceneAsync(
                _characterSelectSceneName,
                LoadSceneMode.Additive
            );

            _isLoading = false;
        }

        /// <summary>
        /// 選択中キャラが変わったとき、ロビー中央のキャラ表示を差し替える。
        /// </summary>
        private void RefreshCharacter(GameObject characterPrefab)
        {
            DestroyCurrentPreview();

            if (characterPrefab == null)
            {
                if (_characterNameText != null)
                {
                    _characterNameText.text = "キャラ未選択";
                }

                return;
            }

            if (_characterNameText != null)
            {
                _characterNameText.text = characterPrefab.name;
            }

            if (_previewRoot == null)
            {
                Debug.LogError("PreviewRoot が設定されていません。");
                return;
            }

            _currentPreviewObject = Instantiate(
                characterPrefab,
                _previewRoot
            );

            HidePreviewCanvases(_currentPreviewObject);

            _currentPreviewObject.transform.localPosition = Vector3.zero;
            _currentPreviewObject.transform.localRotation = Quaternion.identity;
            _currentPreviewObject.transform.localScale = _previewScale;
        }

        private void DestroyCurrentPreview()
        {
            if (_currentPreviewObject == null)
            {
                return;
            }

            Destroy(_currentPreviewObject);
            _currentPreviewObject = null;
        }

        /// <summary>
        /// SceneがUnloadされたときに呼ばれる。
        /// キャラ選択Sceneが閉じられた場合だけ、ロビー中央のキャラ表示を戻す。
        /// </summary>
        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name != _characterSelectSceneName)
            {
                return;
            }

            // キャラ選択Sceneが閉じられたので、
            // ロビー中央の現在選択中キャラとメインUIを再表示する。
            SetPreviewRootVisible(true);
            SetMainPanelVisible(true);

            StartCoroutine(SelectReturnUiNextFrame());
        }

        /// <summary>
        /// ロビー中央のキャラ表示Rootを表示・非表示にする。
        /// </summary>
        private void SetPreviewRootVisible(bool visible)
        {
            if (_previewRoot == null)
            {
                return;
            }

            _previewRoot.gameObject.SetActive(visible);
        }

        /// <summary>
        /// ロビーのメインUIを表示・非表示にする。
        /// </summary>
        private void SetMainPanelVisible(bool visible)
        {
            if (_mainPanel == null)
            {
                return;
            }

            _mainPanel.SetActive(visible);
        }

        private IEnumerator SelectReturnUiNextFrame()
        {
            yield return null;

            SelectUi(_returnSelectable);
        }

        private void SelectUi(Selectable selectable)
        {
            if (selectable == null)
            {
                return;
            }

            if (EventSystem.current == null)
            {
                return;
            }

            if (!selectable.gameObject.activeInHierarchy)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private void ClearUiSelection()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>
        /// プレビュー表示では、キャラPrefab内のCanvasを非表示にする。
        /// HPBarやReloadBarなど、ゲーム中用UIが見えるのを防ぐ。
        /// </summary>
        private void HidePreviewCanvases(GameObject previewObject)
        {
            Canvas[] canvases = previewObject.GetComponentsInChildren<Canvas>(true);

            foreach (Canvas canvas in canvases)
            {
                canvas.gameObject.SetActive(false);
            }
        }
    }
}
