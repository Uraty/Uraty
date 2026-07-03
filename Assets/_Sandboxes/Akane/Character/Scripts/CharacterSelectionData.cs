using UnityEngine;
using Uraty.Shared.Role;
using Uraty.Shared.Team;

namespace Uraty.Feature.Akane_TestCharacter
{
    public sealed class CharacterSelectionData : MonoBehaviour
    {
        [Header("マッチング用キャラクター情報")]
        [SerializeField, Tooltip("このキャラクターPrefabの役職")]
        private RoleType _roleType;

        [SerializeField, Tooltip("このキャラクターPrefabのチームID")]
        private TeamId _teamId;

        public RoleType RoleType => _roleType;
        public TeamId TeamId => _teamId;
    }
}
