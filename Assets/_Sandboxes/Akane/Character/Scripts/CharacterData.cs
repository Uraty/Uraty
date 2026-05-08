using UnityEngine;

namespace Uraty.Feature.Akane_TestCharacter
{
    [CreateAssetMenu(
        fileName = "CharacterData",
        menuName = "Uraty/Character/Character Data"
    )]
    public sealed class CharacterData : ScriptableObject
    {
        [SerializeField] private string _characterId;
        [SerializeField] private string _displayName;
        [SerializeField] private GameObject _previewPrefab;

        public string CharacterId => _characterId;
        public string DisplayName => _displayName;
        public GameObject PreviewPrefab => _previewPrefab;
    }
}
