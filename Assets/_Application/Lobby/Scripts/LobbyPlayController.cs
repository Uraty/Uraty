using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Uraty.Feature.Akane_GameMode;
using Uraty.Feature.GameStart;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビーのPlayButtonを管理するクラス。
    /// 現在選択中のモード情報を保存し、そのモードに対応したSceneへ遷移する。
    /// </summary>
    public sealed class LobbyPlayController : MonoBehaviour
    {
        // MainPanelにあるプレイ開始ボタン。
        [SerializeField] private Button _playButton;

        // 現在選択中のモードを取得するためのController。
        [SerializeField] private LobbyModeSelectController _modeSelectController;

        private void Awake()
        {
            // PlayButtonが押されたらゲーム開始処理を行う。
            _playButton.onClick.AddListener(OnClickPlay);
        }

        private void OnDestroy()
        {
            // 登録したイベントを解除する。
            _playButton.onClick.RemoveListener(OnClickPlay);
        }

        /// <summary>
        /// PlayButton押下時の処理。
        /// 選択中モードを保存して、対応するGameSceneへ遷移する。
        /// </summary>
        private void OnClickPlay()
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

            // BattleScene側で読めるように、選択モードを一時保存する。
            GameStartDataStore.SetSelectedMode(selectedMode);

            Debug.Log($"シーン遷移前: 選択モード = {selectedMode.DisplayName}");

            // 選択モードに設定されているSceneへ遷移する。
            SceneManager.LoadScene(selectedMode.GameSceneName);
        }
    }
}
