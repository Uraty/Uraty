using UnityEngine;

using Uraty.Feature.Akane_GameMode;

namespace Uraty.Application.GameStart
{
    /// <summary>
    /// ロビーから次のシーンへ渡すゲーム開始情報を保持するStore。
    /// ScriptableObjectアセットとしてシーン間で共有する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameStartDataStore",
        menuName = "Uraty/GameStart/Game Start Data Store"
    )]
    public sealed class GameStartDataStore : ScriptableObject
    {
        private GameModeData _selectedMode;

        public bool HasSelectedMode => _selectedMode != null;

        public GameModeData SelectedMode => _selectedMode;

        public void SetSelectedMode(GameModeData mode)
        {
            _selectedMode = mode;
        }

        public void Clear()
        {
            _selectedMode = null;
        }
    }
}
