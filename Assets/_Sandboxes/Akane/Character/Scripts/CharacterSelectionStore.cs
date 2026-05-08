using System;

namespace Uraty.Feature.Akane_TestCharacter
{
    public static class CharacterSelectionStore
    {
        public static event Action<CharacterData> SelectedCharacterChanged;

        public static CharacterData SelectedCharacter
        {
            get; private set;
        }

        public static void SetSelectedCharacter(CharacterData character)
        {
            SelectedCharacter = character;
            SelectedCharacterChanged?.Invoke(character);
        }
    }
}
