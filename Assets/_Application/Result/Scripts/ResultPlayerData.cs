using UnityEngine;
using UnityEngine.UI;

using Uraty.Shared.Entry;
using Uraty.Shared.Role;
using Uraty.Shared.Team;

namespace Uraty.Application.Result
{
    public class ResultPlayerData : MonoBehaviour
    {
        [Header("Result Entry")]
        [SerializeField]
        private ResultSceneEntry _resultSceneEntry;

        [Header("テキスト")]
        [SerializeField]
        private Text[] _resultPlayerDataTexts;

        [Header("キャラクター")]
        [SerializeField]
        private GameObject _assassinPrefab;

        [SerializeField]
        private GameObject _attackerPrefab;

        [SerializeField]
        private GameObject _fighterPrefab;

        [SerializeField]
        private GameObject _sniperPrefab;

        [Header("キャラクター生成位置")]
        [SerializeField]
        private Transform[] _characterSpawnPositions;

        private GameObject[] _playerObjects;

        private void Start()
        {
            int spawnCount = _characterSpawnPositions != null
                ? _characterSpawnPositions.Length
                : 0;

            _playerObjects =
                new GameObject[spawnCount];

            if (_resultSceneEntry == null
                || !_resultSceneEntry.HasEntry)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: ResultSceneEntry に結果データがありません。");

                return;
            }

            int characterCount = Mathf.Min(
                ResultSceneEntry.CharacterCount,
                spawnCount,
                _resultPlayerDataTexts != null
                    ? _resultPlayerDataTexts.Length
                    : 0);

            for (int i = 0; i < characterCount; i++)
            {
                if (!_resultSceneEntry.TryGetCharacter(
                        i,
                        out ResultCharacterEntry entry)
                    || entry == null)
                {
                    continue;
                }

                SetResultPlayerData(
                    i,
                    entry);
            }
        }

        private void SetResultPlayerData(
            int playerIndex,
            ResultCharacterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (playerIndex >= _resultPlayerDataTexts.Length
                || playerIndex >= _characterSpawnPositions.Length)
            {
                Debug.LogWarning(
                    $"範囲外アクセス : {playerIndex}");

                return;
            }

            Text targetText = _resultPlayerDataTexts[playerIndex];

            if (targetText != null)
            {
                targetText.text =
                    $"{entry.RoleType}" +
                    $"\nTEAM\t\t: {entry.TeamId}" +
                    $"\nRESULT\t: {ResolveResultText(entry.TeamId)}" +
                    $"\nWANTED\t: {entry.WantedScore}" +
                    $"\nDAMAGE\t: {Mathf.RoundToInt(entry.DamageDealt)}" +
                    $"\nTAKEN\t\t: {Mathf.RoundToInt(entry.DamageTaken)}" +
                    $"\nHEAL\t\t: {Mathf.RoundToInt(entry.HealingDone)}" +
                    $"\nKILL\t\t: {entry.KillCount}" +
                    $"\nDEATH\t\t: {entry.DeathCount}";
            }

            GameObject prefab = GetCharacterPrefab(entry.RoleType);

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"{entry.RoleType} 用のResult表示Prefabが設定されていません。");

                return;
            }

            _playerObjects[playerIndex] =
                Instantiate(
                    prefab,
                    _characterSpawnPositions[playerIndex].position,
                    _characterSpawnPositions[playerIndex].rotation);

            _playerObjects[playerIndex].AddComponent<RotateObject>();

            Canvas canvas =
                _playerObjects[playerIndex]
                    .GetComponentInChildren<Canvas>(true);

            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }

            int resultFrontLayer = LayerMask.NameToLayer("ResultFrontObj");

            if (resultFrontLayer == -1)
            {
                return;
            }

            SetLayerRecursively(
                _playerObjects[playerIndex],
                resultFrontLayer);
        }

        private GameObject GetCharacterPrefab(RoleType roleType)
        {
            return roleType switch
            {
                RoleType.Assassin => _assassinPrefab,
                RoleType.Attacker => _attackerPrefab,
                RoleType.Fighter => _fighterPrefab,
                RoleType.Sniper => _sniperPrefab,
                _ => _fighterPrefab
            };
        }

        private string ResolveResultText(TeamId teamId)
        {
            if (_resultSceneEntry == null)
            {
                return BattleResultType.None.ToString();
            }

            if (_resultSceneEntry.WinnerTeamId == TeamId.None)
            {
                return BattleResultType.Draw.ToString();
            }

            return _resultSceneEntry.WinnerTeamId == teamId
                ? BattleResultType.Win.ToString()
                : BattleResultType.Lose.ToString();
        }

        private void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null)
            {
                return;
            }

            target.layer = layer;

            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
