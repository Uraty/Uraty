//using UnityEngine;
//using UnityEngine.SceneManagement;
//using UnityEngine.UI;

//using Uraty.Feature.GameStart;

//namespace Uraty.Application.Lobby
//{
//    public sealed class LobbySceneController : MonoBehaviour
//    {
//        [SerializeField] private LobbyModeSelectController _modeSelectController;
//        [SerializeField] private LobbyCharacterSelectController _characterSelectController;
//        [SerializeField] private LobbySettingsController _settingsController;

//        [SerializeField] private Button _playButton;
//        [SerializeField] private string _gameSceneName = "Game";

//        private void Awake()
//        {
//            _playButton.onClick.AddListener(StartGame);
//        }

//        private void StartGame()
//        {
//            if (_characterSelectController.SelectedCharacter == null)
//            {
//                Debug.LogWarning("キャラが選択されていません");
//                return;
//            }

//            GameStartData startData = new GameStartData
//            {
//                SelectedMode = _modeSelectController.SelectedMode,
//                SelectedCharacter = _characterSelectController.SelectedCharacter,
//                SelectedSkin = _characterSelectController.SelectedSkin,
//                Settings = _settingsController.CurrentSettings
//            };

//            GameStartDataStore.Set(startData);

//            SceneManager.LoadScene(_gameSceneName);
//        }
//    }
//}
