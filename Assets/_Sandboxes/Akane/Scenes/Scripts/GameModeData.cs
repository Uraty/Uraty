using UnityEngine;

namespace Uraty.Feature.Akane_GameMode
{
    [CreateAssetMenu(
        fileName = "GameModeData",
        menuName = "Uraty/GameMode/Game Mode Data"
    )]
    public sealed class GameModeData : ScriptableObject
    {
        [SerializeField] private string _modeId;
        [SerializeField] private string _displayName;
        [SerializeField][TextArea] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private string _gameSceneName;

        public string ModeId => _modeId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public string GameSceneName => _gameSceneName;
    }
}
