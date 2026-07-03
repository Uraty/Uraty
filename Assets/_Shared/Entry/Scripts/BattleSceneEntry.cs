using System;

using UnityEngine;

using Uraty.Shared.Role;
using Uraty.Shared.Team;

namespace Uraty.Shared.Entry
{
    [CreateAssetMenu(
        fileName = "BattleSceneEntry",
        menuName = "Uraty/Entry/Battle Scene Entry")]
    public sealed class BattleSceneEntry : ScriptableObject
    {
        public const int PlayerIndex = 0;

        public const int AllyBotStartIndex = 1;
        public const int AllyBotCount = 2;

        public const int EnemyBotStartIndex = 3;
        public const int EnemyBotCount = 3;

        public const int CharacterCount = 6;

        [NonSerialized]
        private bool _hasEntry;

        [NonSerialized]
        private TeamId[] _teamIds;

        [NonSerialized]
        private RoleType[] _roleTypes;

        public bool HasEntry => _hasEntry;

        public void SetEntry(
            TeamId playerTeamId,
            RoleType playerRoleType,
            RoleType[] allyBotRoleTypes,
            TeamId enemyTeamId,
            RoleType[] enemyBotRoleTypes)
        {
            if (allyBotRoleTypes == null
                || allyBotRoleTypes.Length < AllyBotCount)
            {
                Debug.LogError(
                    $"{nameof(BattleSceneEntry)}: 味方BotのRoleTypeが不足しています。");

                Clear();

                return;
            }

            if (enemyBotRoleTypes == null
                || enemyBotRoleTypes.Length < EnemyBotCount)
            {
                Debug.LogError(
                    $"{nameof(BattleSceneEntry)}: 敵BotのRoleTypeが不足しています。");

                Clear();

                return;
            }

            EnsureArraySize();

            _teamIds[PlayerIndex] =
                playerTeamId;

            _roleTypes[PlayerIndex] =
                playerRoleType;

            for (int i = 0; i < AllyBotCount; i++)
            {
                int index =
                    AllyBotStartIndex + i;

                _teamIds[index] =
                    playerTeamId;

                _roleTypes[index] =
                    allyBotRoleTypes[i];
            }

            for (int i = 0; i < EnemyBotCount; i++)
            {
                int index =
                    EnemyBotStartIndex + i;

                _teamIds[index] =
                    enemyTeamId;

                _roleTypes[index] =
                    enemyBotRoleTypes[i];
            }

            _hasEntry =
                true;
        }

        public bool TryConsume(
            out TeamId[] teamIds,
            out RoleType[] roleTypes)
        {
            if (!_hasEntry)
            {
                teamIds =
                    Array.Empty<TeamId>();

                roleTypes =
                    Array.Empty<RoleType>();

                return false;
            }

            EnsureArraySize();

            teamIds =
                new TeamId[CharacterCount];

            roleTypes =
                new RoleType[CharacterCount];

            Array.Copy(
                _teamIds,
                teamIds,
                CharacterCount);

            Array.Copy(
                _roleTypes,
                roleTypes,
                CharacterCount);

            Clear();

            return true;
        }

        public void Clear()
        {
            _hasEntry =
                false;

            EnsureArraySize();

            for (int i = 0; i < CharacterCount; i++)
            {
                _teamIds[i] =
                    default;

                _roleTypes[i] =
                    default;
            }
        }

        private void EnsureArraySize()
        {
            if (_teamIds == null
                || _teamIds.Length != CharacterCount)
            {
                _teamIds =
                    new TeamId[CharacterCount];
            }

            if (_roleTypes == null
                || _roleTypes.Length != CharacterCount)
            {
                _roleTypes =
                    new RoleType[CharacterCount];
            }
        }
    }
}
