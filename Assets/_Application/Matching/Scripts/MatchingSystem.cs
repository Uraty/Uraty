using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using Uraty.Shared.Role;
using Uraty.Shared.Team;
using Uraty.Shared.Entry;
using Uraty.Shared.Setting;
using Uraty.Systems.Input;
using Uraty.Feature.Akane_TestCharacter;

namespace Uraty.Application.Matching
{
    public sealed class MatchingSystem : MonoBehaviour
    {
        private const float MatchingDurationSeconds = 3.0f;

        [Header("入力管理")]
        [SerializeField] private GameInput _gameInput;

        [Header("マッチング情報")]
        [SerializeField] private MatchingContext _matchingContext;

        [Header("ゲーム設定")]
        [SerializeField] private CharacterSelectionStore _characterSelectionStore;

        [Header("敵チーム設定")]
        [SerializeField, Tooltip("敵Botに設定するチームID")]
        private TeamId _enemyTeamId;

        [Header("役職候補")]
        [SerializeField, Tooltip("Botにランダム割り当てできる役職候補")]
        private RoleType[] _assignableRoleIds;

        [Header("Battle Scene Entry")]
        [SerializeField] private BattleSceneEntry _battleSceneEntry;

        private GameSettingsData _gameSettingsData;
        private float _elapsedSeconds;
        private bool _hasLoadedScene;
        private bool _isInitialized;

        private void Awake()
        {
            Debug.Log($"{nameof(MatchingSystem)}: Awake");

            _elapsedSeconds = 0.0f;
            _hasLoadedScene = false;
            _isInitialized = false;

            if (!CanInitialize())
            {
                enabled = false;
                return;
            }

            _gameInput.EnableUIInput();

            ApplyGameSettings();

            if (!TryApplySelectedCharacterData())
            {
                enabled = false;
                return;
            }

            _matchingContext.ClearBotData();

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (_hasLoadedScene)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;

            if (_elapsedSeconds < MatchingDurationSeconds)
            {
                return;
            }

            CompleteMatching();
        }

        private bool CanInitialize()
        {
            if (_gameInput == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: GameInputが設定されていません。", this);
                return false;
            }

            if (_matchingContext == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: MatchingContextが設定されていません。", this);
                return false;
            }

            if (_characterSelectionStore == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: CharacterSelectionStoreが設定されていません。", this);
                return false;
            }

            if (_assignableRoleIds == null || _assignableRoleIds.Length == 0)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: 役職候補が設定されていません。", this);
                return false;
            }

            if (_battleSceneEntry == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: BattleSceneEntryが設定されていません。", this);
                return false;
            }

            return true;
        }

        private void ApplyGameSettings()
        {
            _gameSettingsData = GameSettingsStore.Load();

            _matchingContext.SetMouseSensitivity(_gameSettingsData.MouseSensitivity);
            _matchingContext.SetStickSensitivityKey(_gameSettingsData.StickSensitivity);
            _matchingContext.SetKeyMouseDeadZone(_gameSettingsData.KeyMouseDeadZone);
            _matchingContext.SetStickDeadZone(_gameSettingsData.StickDeadZone);
            _matchingContext.SetSeVolume(_gameSettingsData.SeVolume);
            _matchingContext.SetBgmVolume(_gameSettingsData.BgmVolume);
        }

        private bool TryApplySelectedCharacterData()
        {
            GameObject selectedCharacterPrefab = _characterSelectionStore.SelectedCharacterPrefab;

            if (selectedCharacterPrefab == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: 選択中のキャラクターPrefabが設定されていません。", this);
                return false;
            }

            CharacterSelectionData characterSelectionData =
                selectedCharacterPrefab.GetComponentInChildren<CharacterSelectionData>(true);

            if (characterSelectionData == null)
            {
                Debug.LogError(
                    $"{nameof(MatchingSystem)}: 選択中のキャラクターPrefabに{nameof(CharacterSelectionData)}が見つかりません。Prefabに{nameof(CharacterSelectionData)}を追加してください。",
                    selectedCharacterPrefab);

                return false;
            }

            _matchingContext.SetPlayerData(
                characterSelectionData.TeamId,
                characterSelectionData.RoleType);

            Debug.Log(
                $"{nameof(MatchingSystem)}: PlayerData設定完了 Team={characterSelectionData.TeamId}, Role={characterSelectionData.RoleType}");

            return true;
        }

        private void CompleteMatching()
        {
            _hasLoadedScene = true;

            if (!TryAssignBotData())
            {
                Debug.LogError($"{nameof(MatchingSystem)}: Botの役職割り当てに失敗したため、BattleSceneへ遷移しません。", this);
                return;
            }

            LoadBattleScene();
        }

        private bool TryAssignBotData()
        {
            if (_matchingContext == null)
            {
                return false;
            }

            List<RoleType> uniqueRoleIds = CreateUniqueRoleIdList();

            if (uniqueRoleIds.Count < MatchingContext.SecondaryBotCount)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: 敵チーム3体に重複なしで割り当てるには、役職候補が3種類以上必要です。", this);
                return false;
            }

            RoleType playerRoleId = _matchingContext.PlayerRoleId;

            List<RoleType> allyCandidateRoleIds = new List<RoleType>(uniqueRoleIds);
            allyCandidateRoleIds.Remove(playerRoleId);

            if (allyCandidateRoleIds.Count < MatchingContext.PrimaryBotCount)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: 味方Bot2体に重複なしで割り当てるには、プレイヤー役職を除いて2種類以上の役職候補が必要です。", this);
                return false;
            }

            RoleType[] allyRoleIds = PickRandomRoleIds(
                allyCandidateRoleIds,
                MatchingContext.PrimaryBotCount);

            RoleType[] enemyRoleIds = PickRandomRoleIds(
                uniqueRoleIds,
                MatchingContext.SecondaryBotCount);

            ApplyMatchingContextBotData(allyRoleIds, enemyRoleIds);
            ApplyBattleSceneEntry(allyRoleIds, enemyRoleIds);
            DebugBotData(allyRoleIds, enemyRoleIds);

            return true;
        }

        private List<RoleType> CreateUniqueRoleIdList()
        {
            List<RoleType> uniqueRoleIds = new List<RoleType>();

            if (_assignableRoleIds == null)
            {
                return uniqueRoleIds;
            }

            foreach (RoleType roleId in _assignableRoleIds)
            {
                if (uniqueRoleIds.Contains(roleId))
                {
                    continue;
                }

                uniqueRoleIds.Add(roleId);
            }

            return uniqueRoleIds;
        }

        private RoleType[] PickRandomRoleIds(List<RoleType> sourceRoleIds, int pickCount)
        {
            if (sourceRoleIds.Count < pickCount)
            {
                return new RoleType[0];
            }

            List<RoleType> shuffledRoleIds = new List<RoleType>(sourceRoleIds);

            for (int i = 0; i < pickCount; i++)
            {
                int randomIndex = Random.Range(i, shuffledRoleIds.Count);

                RoleType temporaryRoleId = shuffledRoleIds[i];
                shuffledRoleIds[i] = shuffledRoleIds[randomIndex];
                shuffledRoleIds[randomIndex] = temporaryRoleId;
            }

            RoleType[] resultRoleIds = new RoleType[pickCount];

            for (int i = 0; i < pickCount; i++)
            {
                resultRoleIds[i] = shuffledRoleIds[i];
            }

            return resultRoleIds;
        }

        private void ApplyMatchingContextBotData(
            RoleType[] allyRoleIds,
            RoleType[] enemyRoleIds)
        {
            _matchingContext.ClearBotData();

            for (int i = 0; i < allyRoleIds.Length; i++)
            {
                _matchingContext.SetBotData(
                    i,
                    _matchingContext.PlayerTeamId,
                    allyRoleIds[i]);
            }

            for (int i = 0; i < enemyRoleIds.Length; i++)
            {
                int botIndex = MatchingContext.PrimaryBotCount + i;

                _matchingContext.SetBotData(
                    botIndex,
                    _enemyTeamId,
                    enemyRoleIds[i]);
            }
        }

        private void ApplyBattleSceneEntry(
            RoleType[] allyRoleIds,
            RoleType[] enemyRoleIds)
        {
            _battleSceneEntry.SetEntry(
                _matchingContext.PlayerTeamId,
                _matchingContext.PlayerRoleId,
                allyRoleIds,
                _enemyTeamId,
                enemyRoleIds);
        }

        private void LoadBattleScene()
        {
            if (_matchingContext.GameModeData == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: GameModeDataが設定されていません。", this);
                return;
            }

            string targetSceneName = "BattleScene";

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError($"{nameof(MatchingSystem)}: GameModeDataの遷移先Scene名が空です。", this);
                return;
            }

            SceneManager.LoadScene(targetSceneName);
        }

        private void DebugBotData(RoleType[] allyRoleIds, RoleType[] enemyRoleIds)
        {
            Debug.Log($"Player: Team={_matchingContext.PlayerTeamId}, Role={_matchingContext.PlayerRoleId}");

            for (int i = 0; i < allyRoleIds.Length; i++)
            {
                Debug.Log($"AllyBot{i}: Team={_matchingContext.PlayerTeamId}, Role={allyRoleIds[i]}");
            }

            for (int i = 0; i < enemyRoleIds.Length; i++)
            {
                Debug.Log($"EnemyBot{i}: Team={_enemyTeamId}, Role={enemyRoleIds[i]}");
            }
        }
    }
}
