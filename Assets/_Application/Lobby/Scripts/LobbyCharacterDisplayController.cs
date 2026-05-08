using System.Collections;

using TMPro;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Uraty.Feature.Akane_TestCharacter;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビー画面中央に、現在選択中のキャラPrefabを表示するクラス。
    /// キャラを押したら、キャラ選択用Additive Sceneを開く。
    /// </summary>
    public sealed class LobbyCharacterDisplayController : MonoBehaviour
    {
        [Header("Default")]
        // まだキャラが選択されていない場合に表示する初期キャラ。
        [SerializeField] private CharacterData _defaultCharacter;

        [Header("View")]
        // キャラPrefabを生成する位置。
        [SerializeField] private Transform _previewRoot;

        // 現在表示中のキャラを押すためのボタン。
        // 透明ボタンとしてキャラ表示部分に重ねる想定
        [SerializeField] private Button _currentCharacterButton;

        // 現在選択中のキャラ名を表示するText。
        [SerializeField] private TextMeshProUGUI _characterNameText;

        [Header("Scene")]
        // Additiveで読み込むキャラ選択Scene名
        [SerializeField] private string _characterSelectSceneName = "LobbyCharacterSelectScene";

        // 現在ロビーに表示しているキャラPrefabの実体。
        private GameObject _currentPreviewObject;

        // キャラ選択Sceneの多重読み込みを防ぐためのフラグ。
        private bool _isLoading;

        private void Awake()
        {
            // キャラ表示部分を押したら、キャラ選択Sceneを開く。
            _currentCharacterButton.onClick.AddListener(OpenCharacterSelectScene);

            // 選択キャラが変更されたら、ロビー中央の表示を更新する。
            CharacterSelectionStore.SelectedCharacterChanged += RefreshCharacter;

            // まだ選択キャラが存在しない場合、初期キャラを設定する。
            if (CharacterSelectionStore.SelectedCharacter == null)
            {
                CharacterSelectionStore.SetSelectedCharacter(_defaultCharacter);
            }
            else
            {
                // すでに選択済みのキャラがある場合は、そのキャラを表示する。
                RefreshCharacter(CharacterSelectionStore.SelectedCharacter);
            }
        }

        private void OnDestroy()
        {
            // 登録したイベントは破棄時に解除する。
            _currentCharacterButton.onClick.RemoveListener(OpenCharacterSelectScene);
            CharacterSelectionStore.SelectedCharacterChanged -= RefreshCharacter;
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

            yield return SceneManager.LoadSceneAsync(
                _characterSelectSceneName,
                LoadSceneMode.Additive
            );

            _isLoading = false;
        }

        /// <summary>
        /// 選択中キャラが変わったとき、ロビー中央のキャラ表示を差し替える。
        /// </summary>
        private void RefreshCharacter(CharacterData character)
        {
            // 古い表示用Prefabがあれば削除する。
            if (_currentPreviewObject != null)
            {
                Destroy(_currentPreviewObject);
                _currentPreviewObject = null;
            }

            if (character == null)
            {
                if (_characterNameText != null)
                {
                    _characterNameText.text = "キャラ未選択";
                }

                return;
            }

            // キャラ名を表示する。
            if (_characterNameText != null)
            {
                _characterNameText.text = character.DisplayName;
            }

            // 表示用Prefabが設定されていない場合は警告を出す。
            if (character.PreviewPrefab == null)
            {
                Debug.LogWarning($"{character.DisplayName} に PreviewPrefab が設定されていません。");
                return;
            }

            // 選択されたキャラの表示用Prefabをロビー中央に生成する。
            _currentPreviewObject = Instantiate(
                character.PreviewPrefab,
                _previewRoot.position,
                _previewRoot.rotation,
                _previewRoot
            );
        }
    }
}
