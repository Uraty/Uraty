using R3;

using UnityEngine;

namespace Uraty.Feature.Akane_TestCharacter
{
    [CreateAssetMenu(
        fileName = "CharacterSelectionStore",
        menuName = "Uraty/Character/Character Selection Store")]
    public sealed class CharacterSelectionStore : ScriptableObject
    {
        [Header("Default")]
        [SerializeField] private GameObject _defaultCharacterPrefab;

        [Header("Current")]
        [SerializeField] private GameObject _selectedCharacterPrefab;

        private readonly Subject<GameObject> _selectedCharacterPrefabChangedSubject = new();

        public GameObject SelectedCharacterPrefab =>
            _selectedCharacterPrefab != null
                ? _selectedCharacterPrefab
                : _defaultCharacterPrefab;

        public Observable<GameObject> SelectedCharacterPrefabChangedStream =>
            _selectedCharacterPrefabChangedSubject;

        public void SetSelectedCharacterPrefab(GameObject characterPrefab)
        {
            if (_selectedCharacterPrefab == characterPrefab)
            {
                return;
            }

            _selectedCharacterPrefab = characterPrefab;
            _selectedCharacterPrefabChangedSubject.OnNext(SelectedCharacterPrefab);
        }

        private void OnDestroy()
        {
            _selectedCharacterPrefabChangedSubject.Dispose();
        }
    }
}
