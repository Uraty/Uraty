using Uraty.Feature.Akane_GameMode;

namespace Uraty.Feature.GameStart
{
    public static class GameStartDataStore
    {
        public static GameModeData SelectedMode
        {
            get; private set;
        }

        public static void SetSelectedMode(GameModeData mode)
        {
            SelectedMode = mode;
        }

        public static void Clear()
        {
            SelectedMode = null;
        }
    }
}
