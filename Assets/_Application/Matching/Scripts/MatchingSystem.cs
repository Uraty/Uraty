using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using Uraty.Shared.Role;
using Uraty.Shared.Team;
using Uraty.Shared.Entry;
using Uraty.Systems.Input;

namespace Uraty.Application.Matching
{
    public sealed class MatchingSystem : MonoBehaviour
    {
        private const float MatchingDurationSeconds = 3.0f;

        [Header("入力管理")]
        [SerializeField] private GameInput _gameInput;

        [Header("マッチング情報")]
        [SerializeField] private MatchingContext _matchingContext;

        [Header("敵チーム設定")]
        [SerializeField, Tooltip("敵Botに設定するチームID")] private TeamId _enemyTeamId;

        [Header("役職候補")]
        [SerializeField, Tooltip("Botにランダム割り当てできる役職候補")]
        private RoleType[] _assignableRoleIds;

        [Header("Battle Scene Entry")]
        [SerializeField] private BattleSceneEntry _battleSceneEntry;

        private float _elapsedSeconds;
        private bool _hasLoadedScene;

        private void Awake()
        {
            Debug.Log($"{nameof(MatchingSystem)}: Awake");
            if (_gameInput == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: GameInputが設定されていません。");
                return;
            }

            if (_matchingContext == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: MatchingContextが設定されていません。");
                return;
            }

            if (_assignableRoleIds == null || _assignableRoleIds.Length == 0)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: 役職候補が設定されていません。");
                return;
            }

            if (_battleSceneEntry == null)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: BattleSceneEntryが設定されていません。");
                return;
            }

            _gameInput.EnableUIInput();

            _elapsedSeconds = 0.0f;
            _hasLoadedScene = false;
        }

        private void Update()
        {
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

        private void CompleteMatching()
        {
            _hasLoadedScene = true;

            if (!TryAssignBotData())
            {
                Debug.LogError($"{nameof(MatchingSystem)}: Botの役職割り当てに失敗したため、BattleSceneへ遷移しません。");
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
                Debug.LogError($"{nameof(MatchingSystem)}: 敵チーム3体に重複なしで割り当てるには、役職候補が3種類以上必要です。");
                return false;
            }

            RoleType playerRoleId = _matchingContext.PlayerRoleId;

            List<RoleType> allyCandidateRoleIds = new List<RoleType>(uniqueRoleIds);
            allyCandidateRoleIds.Remove(playerRoleId);

            if (allyCandidateRoleIds.Count < MatchingContext.PrimaryBotCount)
            {
                Debug.LogError($"{nameof(MatchingSystem)}: 味方Bot2体に重複なしで割り当てるには、プレイヤー役職を除いて2種類以上の役職候補が必要です。");
                return false;
            }

            RoleType[] allyRoleIds = PickRandomRoleIds(
                allyCandidateRoleIds,
                MatchingContext.PrimaryBotCount);

            RoleType[] enemyRoleIds = PickRandomRoleIds(
                uniqueRoleIds,
                MatchingContext.SecondaryBotCount);
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
                Debug.LogError($"{nameof(MatchingSystem)}: GameModeDataが設定されていません。");
                return;
            }

            string targetSceneName = _matchingContext.GameModeData.GameSceneName;

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError($"{nameof(MatchingSystem)}: GameModeDataの遷移先Scene名が空です。");
                return;
            }

            SceneManager.LoadScene("BattleScene");
        }

        private void DebugBotData(RoleType[] allyRoleIds, RoleType[] enemyRoleIds)
        {
            Debug.Log($"PlayerRole: {_matchingContext.PlayerRoleId}");

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
