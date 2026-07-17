using UnityEngine;
using UnityEngine.UI;

using Uraty.Shared.Entry;
using Uraty.Shared.Role;

namespace Uraty.Application.Result
{
    public class ResultPlayerData : MonoBehaviour
    {
        [Header("Result Entry")]
        [SerializeField]
        private ResultSceneEntry _resultSceneEntry;

        [Header("プレイヤー情報テキスト")]
        [SerializeField]
        private Text[] _resultPlayerDataTexts;

        [Header("勝敗テキスト")]
        [SerializeField]
        private Text _resultText;

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

            _playerObjects = new GameObject[spawnCount];

            if (_resultSceneEntry == null)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: ResultSceneEntryが設定されていません。");

                return;
            }

            if (!_resultSceneEntry.HasEntry)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: ResultSceneEntryに結果データがありません。");

                return;
            }

            // 0番目のキャラクターのWantedScoreだけを使って、
            // WIN・LOSE・DRAWを表示する
            SetResultTextFromFirstCharacter();

            int resultTextCount = _resultPlayerDataTexts != null
                ? _resultPlayerDataTexts.Length
                : 0;

            int characterCount = Mathf.Min(
                ResultSceneEntry.CharacterCount,
                spawnCount,
                resultTextCount);

            for (int playerIndex = 0;
                 playerIndex < characterCount;
                 playerIndex++)
            {
                if (!_resultSceneEntry.TryGetCharacter(
                        playerIndex,
                        out ResultCharacterEntry entry)
                    || entry == null)
                {
                    Debug.LogWarning(
                        $"{nameof(ResultPlayerData)}: " +
                        $"{playerIndex}番目のキャラクターデータを取得できませんでした。");

                    continue;
                }

                SetResultPlayerData(
                    playerIndex,
                    entry);
            }
        }

        /// <summary>
        /// 0番目のキャラクターが持つWantedScoreを
        /// リザルトの勝敗テキストへ反映します。
        /// </summary>
        private void SetResultTextFromFirstCharacter()
        {
            if (_resultText == null)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: ResultTextが設定されていません。");

                return;
            }

            if (!_resultSceneEntry.TryGetCharacter(
                    0,
                    out ResultCharacterEntry firstEntry)
                || firstEntry == null)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: " +
                    "0番目のキャラクターデータを取得できませんでした。");

                _resultText.text = string.Empty;

                return;
            }

            _resultText.text = firstEntry.WantedScore switch
            {
                3 => $"DRAW",
                2 => $"LOSE",
                1 => $"WIN",
                _ => $"UNKNOWN"
            };

            Debug.Log(
                $"{nameof(ResultPlayerData)}: " +
                $"WantedScore = {firstEntry.WantedScore}, " +
                $"ResultText = {_resultText.text}");
        }

        private void SetResultPlayerData(
            int playerIndex,
            ResultCharacterEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (_resultPlayerDataTexts == null
                || _characterSpawnPositions == null)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: " +
                    "プレイヤー情報テキストまたは生成位置が設定されていません。");

                return;
            }

            if (playerIndex < 0
                || playerIndex >= _resultPlayerDataTexts.Length
                || playerIndex >= _characterSpawnPositions.Length
                || playerIndex >= _playerObjects.Length)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: " +
                    $"範囲外アクセスが発生しました。Index = {playerIndex}");

                return;
            }

            Text targetText =
                _resultPlayerDataTexts[playerIndex];

            if (targetText != null)
            {
                targetText.text =
                    $"{entry.RoleType}" +
                    $"\n: {entry.KillCount}" +
                    $"\t\t\t   : {entry.DeathCount}" +
                    $"\n: {Mathf.RoundToInt(entry.DamageDealt)}" +
                    $"\n: {Mathf.RoundToInt(entry.DamageTaken)}" +
                    $"\n: {Mathf.RoundToInt(entry.HealingDone)}";
            }

            GameObject characterPrefab =
                GetCharacterPrefab(entry.RoleType);

            if (characterPrefab == null)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: " +
                    $"{entry.RoleType}用のResult表示Prefabが設定されていません。");

                return;
            }

            Transform spawnPosition =
                _characterSpawnPositions[playerIndex];

            if (spawnPosition == null)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: " +
                    $"{playerIndex}番目の生成位置が設定されていません。");

                return;
            }

            _playerObjects[playerIndex] =
                Instantiate(
                    characterPrefab,
                    spawnPosition.position,
                    spawnPosition.rotation);

            _playerObjects[playerIndex]
                .AddComponent<RotateObject>();

            Canvas characterCanvas =
                _playerObjects[playerIndex]
                    .GetComponentInChildren<Canvas>(true);

            if (characterCanvas != null)
            {
                characterCanvas.gameObject.SetActive(false);
            }

            int resultFrontLayer =
                LayerMask.NameToLayer("ResultFrontObj");

            if (resultFrontLayer == -1)
            {
                Debug.LogWarning(
                    $"{nameof(ResultPlayerData)}: " +
                    "ResultFrontObjレイヤーが存在しません。");

                return;
            }

            SetLayerRecursively(
                _playerObjects[playerIndex],
                resultFrontLayer);
        }

        private GameObject GetCharacterPrefab(
            RoleType roleType)
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

        private void SetLayerRecursively(
            GameObject target,
            int layer)
        {
            if (target == null)
            {
                return;
            }

            target.layer = layer;

            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(
                    child.gameObject,
                    layer);
            }
        }
    }
}
