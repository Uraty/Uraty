using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Uraty.Feature.Akane_GameMode;
using Uraty.Application.GameStart;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビーのPlayButtonを管理するクラス。
    /// 現在選択中のモード情報を保存し、そのモードに対応したSceneへ遷移する。
    /// </summary>
    public sealed class LobbyPlayController : MonoBehaviour
    {
        // MainPanelにあるプレイ開始ボタン。
        [Header("Button")]
        [SerializeField] private Button _playButton;

        // 現在選択中のモードを取得するためのController。
        [Header("Controllers")]
        [SerializeField] private LobbyModeSelectController _modeSelectController;

        [Header("Store")]
        [SerializeField] private GameStartDataStore _gameStartDataStore;

        private void OnEnable()
        {
            _playButton.onClick.AddListener(HandlePlayButtonClicked);
        }

        private void OnDisable()
        {
            _playButton.onClick.RemoveListener(HandlePlayButtonClicked);
        }

        /// <summary>
        /// PlayButton押下時の処理。
        /// 選択中モードを保存して、対応するGameSceneへ遷移する。
        /// </summary>
        private void HandlePlayButtonClicked()
        {
            GameModeData selectedMode = _modeSelectController.SelectedMode;

            if (selectedMode == null)
            {
                Debug.LogWarning("モードが選択されていません。");
                return;
            }

            if (string.IsNullOrEmpty(selectedMode.GameSceneName))
            {
                Debug.LogWarning($"{selectedMode.DisplayName} の遷移先シーン名が設定されていません。");
                return;
            }

            if (_gameStartDataStore == null)
            {
                Debug.LogError("GameStartDataStore が設定されていません。");
                return;
            }

            _gameStartDataStore.SetSelectedMode(selectedMode);

            Debug.Log($"シーン遷移前: 選択モード = {selectedMode.DisplayName}");

            SceneManager.LoadScene(selectedMode.GameSceneName);
        }
    }
}
