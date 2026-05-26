using R3;

using UnityEngine;

using Uraty.Feature.Akane_TestCharacter;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビー内で選択中のキャラを保持するStore。
    /// ScriptableObjectアセットとしてシーン間で共有する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterSelectionStore",
        menuName = "Uraty/Lobby/Character Selection Store"
    )]
    public sealed class CharacterSelectionStore : ScriptableObject
    {
        [SerializeField] private CharacterData _defaultCharacter;

        private readonly Subject<CharacterData> _selectedCharacterChangedSubject = new();

        private CharacterData _selectedCharacter;

        public CharacterData SelectedCharacter
        {
            get
            {
                if (_selectedCharacter != null)
                {
                    return _selectedCharacter;
                }

                return _defaultCharacter;
            }
        }

        public Observable<CharacterData> SelectedCharacterChangedStream =>
            _selectedCharacterChangedSubject;

        public void SetSelectedCharacter(CharacterData character)
        {
            if (character == null)
            {
                return;
            }

            _selectedCharacter = character;
            PublishSelectedCharacterChanged(character);
        }

        public void Clear()
        {
            _selectedCharacter = null;

            if (_defaultCharacter != null)
            {
                PublishSelectedCharacterChanged(_defaultCharacter);
            }
        }

        private void PublishSelectedCharacterChanged(CharacterData character)
        {
            _selectedCharacterChangedSubject.OnNext(character);
        }
    }
}
