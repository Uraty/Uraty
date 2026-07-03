using System;
using System.Collections.Generic;

using UnityEngine;

using Uraty.Shared.Role;
using Uraty.Shared.Team;

namespace Uraty.Shared.Entry
{
    [CreateAssetMenu(
        fileName = "ResultSceneEntry",
        menuName = "Uraty/Entry/Result Scene Entry")]
    public sealed class ResultSceneEntry : ScriptableObject
    {
        private const string DefaultResourcesPath = "ResultSceneEntry";

        public const int CharacterCount = BattleSceneEntry.CharacterCount;

        [NonSerialized]
        private bool _hasEntry;

        [NonSerialized]
        private ResultCharacterEntry[] _characterEntries;

        [NonSerialized]
        private TeamId _playerTeamId = TeamId.None;

        [NonSerialized]
        private TeamId _winnerTeamId = TeamId.None;

        [NonSerialized]
        private BattleResultType _playerResultType = BattleResultType.None;

        [NonSerialized]
        private int _primaryTeamScore;

        [NonSerialized]
        private int _secondaryTeamScore;

        public bool HasEntry => _hasEntry;
        public TeamId PlayerTeamId => _playerTeamId;
        public TeamId WinnerTeamId => _winnerTeamId;
        public BattleResultType PlayerResultType => _playerResultType;

        public IReadOnlyList<ResultCharacterEntry> Characters
        {
            get
            {
                EnsureArraySize();
                return _characterEntries;
            }
        }

        public void Clear()
        {
            EnsureArraySize();

            _hasEntry = false;
            _playerTeamId = TeamId.None;
            _winnerTeamId = TeamId.None;
            _playerResultType = BattleResultType.None;
            _primaryTeamScore = 0;
            _secondaryTeamScore = 0;

            for (int i = 0; i < CharacterCount; i++)
            {
                _characterEntries[i].Clear(i);
            }
        }

        public void SetCharacterIdentity(
            int characterIndex,
            TeamId teamId,
            RoleType roleType,
            bool isPlayer)
        {
            if (!IsValidCharacterIndex(characterIndex))
            {
                Debug.LogWarning(
                    $"{nameof(ResultSceneEntry)}: CharacterIndex が範囲外です。Index={characterIndex}");

                return;
            }

            EnsureArraySize();

            _hasEntry = true;

            _characterEntries[characterIndex].SetIdentity(
                characterIndex,
                teamId,
                roleType,
                isPlayer);

            if (isPlayer)
            {
                _playerTeamId = teamId;
                RefreshPlayerResultType();
            }
        }

        public void AddDamageDealt(
            int characterIndex,
            float amount)
        {
            if (!TryGetMutableCharacter(
                    characterIndex,
                    out ResultCharacterEntry entry))
            {
                return;
            }

            entry.AddDamageDealt(amount);
            _hasEntry = true;
        }

        public void AddDamageTaken(
            int characterIndex,
            float amount)
        {
            if (!TryGetMutableCharacter(
                    characterIndex,
                    out ResultCharacterEntry entry))
            {
                return;
            }

            entry.AddDamageTaken(amount);
            _hasEntry = true;
        }

        public void AddHealingDone(
            int characterIndex,
            float amount)
        {
            if (!TryGetMutableCharacter(
                    characterIndex,
                    out ResultCharacterEntry entry))
            {
                return;
            }

            entry.AddHealingDone(amount);
            _hasEntry = true;
        }

        public void AddKill(
            int characterIndex)
        {
            if (!TryGetMutableCharacter(
                    characterIndex,
                    out ResultCharacterEntry entry))
            {
                return;
            }

            entry.AddKill();
            _hasEntry = true;
        }

        public void AddDeath(
            int characterIndex)
        {
            if (!TryGetMutableCharacter(
                    characterIndex,
                    out ResultCharacterEntry entry))
            {
                return;
            }

            entry.AddDeath();
            _hasEntry = true;
        }

        public void SetWantedScore(
            int characterIndex,
            int score)
        {
            if (!TryGetMutableCharacter(
                    characterIndex,
                    out ResultCharacterEntry entry))
            {
                return;
            }

            entry.SetWantedScore(score);
            _hasEntry = true;
        }

        public void SetTeamScore(
            TeamId teamId,
            int score)
        {
            int validScore = Mathf.Max(0, score);

            switch (teamId)
            {
                case TeamId.Primary:
                    _primaryTeamScore = validScore;
                    break;

                case TeamId.Secondary:
                    _secondaryTeamScore = validScore;
                    break;

                default:
                    return;
            }

            _hasEntry = true;
        }

        public int GetTeamScoreOrDefault(TeamId teamId)
        {
            return teamId switch
            {
                TeamId.Primary => _primaryTeamScore,
                TeamId.Secondary => _secondaryTeamScore,
                _ => 0
            };
        }

        public void SetWinnerTeamId(TeamId winnerTeamId)
        {
            _winnerTeamId = winnerTeamId;
            _hasEntry = true;

            RefreshPlayerResultType();
        }

        public bool TryGetCharacter(
            int characterIndex,
            out ResultCharacterEntry entry)
        {
            EnsureArraySize();

            if (!IsValidCharacterIndex(characterIndex))
            {
                entry = null;
                return false;
            }

            entry = _characterEntries[characterIndex];
            return entry != null;
        }

        private bool TryGetMutableCharacter(
            int characterIndex,
            out ResultCharacterEntry entry)
        {
            EnsureArraySize();

            if (!IsValidCharacterIndex(characterIndex))
            {
                entry = null;
                return false;
            }

            entry = _characterEntries[characterIndex];

            if (entry == null)
            {
                entry = new ResultCharacterEntry(characterIndex);
                _characterEntries[characterIndex] = entry;
            }

            return true;
        }

        private void RefreshPlayerResultType()
        {
            if (_winnerTeamId == TeamId.None)
            {
                _playerResultType = BattleResultType.Draw;
                return;
            }

            if (_playerTeamId == TeamId.None)
            {
                _playerResultType = BattleResultType.None;
                return;
            }

            _playerResultType = _winnerTeamId == _playerTeamId
                ? BattleResultType.Win
                : BattleResultType.Lose;
        }

        private void EnsureArraySize()
        {
            if (_characterEntries == null
                || _characterEntries.Length != CharacterCount)
            {
                _characterEntries = new ResultCharacterEntry[CharacterCount];
            }

            for (int i = 0; i < CharacterCount; i++)
            {
                if (_characterEntries[i] == null)
                {
                    _characterEntries[i] = new ResultCharacterEntry(i);
                }
            }
        }

        private static bool IsValidCharacterIndex(int characterIndex)
        {
            return characterIndex >= 0
                   && characterIndex < CharacterCount;
        }
    }

    public enum BattleResultType
    {
        None = 0,
        Win = 1,
        Lose = 2,
        Draw = 3
    }

    [Serializable]
    public sealed class ResultCharacterEntry
    {
        [SerializeField]
        private int _characterIndex;

        [SerializeField]
        private TeamId _teamId;

        [SerializeField]
        private RoleType _roleType;

        [SerializeField]
        private bool _isPlayer;

        [SerializeField]
        private float _damageDealt;

        [SerializeField]
        private float _damageTaken;

        [SerializeField]
        private float _healingDone;

        [SerializeField]
        private int _killCount;

        [SerializeField]
        private int _deathCount;

        [SerializeField]
        private int _wantedScore;

        public ResultCharacterEntry(int characterIndex)
        {
            Clear(characterIndex);
        }

        public int CharacterIndex => _characterIndex;
        public TeamId TeamId => _teamId;
        public RoleType RoleType => _roleType;
        public bool IsPlayer => _isPlayer;
        public float DamageDealt => _damageDealt;
        public float DamageTaken => _damageTaken;
        public float HealingDone => _healingDone;
        public int KillCount => _killCount;
        public int DeathCount => _deathCount;
        public int WantedScore => _wantedScore;

        public void Clear(int characterIndex)
        {
            _characterIndex = characterIndex;
            _teamId = TeamId.None;
            _roleType = default;
            _isPlayer = false;
            _damageDealt = 0f;
            _damageTaken = 0f;
            _healingDone = 0f;
            _killCount = 0;
            _deathCount = 0;
            _wantedScore = 0;
        }

        public void SetIdentity(
            int characterIndex,
            TeamId teamId,
            RoleType roleType,
            bool isPlayer)
        {
            _characterIndex = characterIndex;
            _teamId = teamId;
            _roleType = roleType;
            _isPlayer = isPlayer;
        }

        public void AddDamageDealt(float amount)
        {
            _damageDealt += Mathf.Max(0f, amount);
        }

        public void AddDamageTaken(float amount)
        {
            _damageTaken += Mathf.Max(0f, amount);
        }

        public void AddHealingDone(float amount)
        {
            _healingDone += Mathf.Max(0f, amount);
        }

        public void AddKill()
        {
            _killCount++;
        }

        public void AddDeath()
        {
            _deathCount++;
        }

        public void SetWantedScore(int score)
        {
            _wantedScore = Mathf.Max(0, score);
        }
    }
}
