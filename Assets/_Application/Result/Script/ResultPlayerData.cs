using UnityEngine;
using UnityEngine.UI;

namespace Uraty.Features.Result
{
    public class ResultPlayerData : MonoBehaviour
    {
        [Header("テキスト")]
        [SerializeField] private Text[] _resultPlayerDataTexts;

        [Header("キャラクター")]
        [SerializeField] private GameObject _assassinPrefab;

        [SerializeField] private GameObject _attackerPrefab;

        [SerializeField] private GameObject _fighterPrefab;

        [SerializeField] private GameObject _sniperPrefab;

        [Header("キャラクター生成位置")]
        [SerializeField] private Transform[] _characterSpawnPositions;

        private GameObject[] _playerObjects;

        private void Start()
        {
            _playerObjects =
                new GameObject[_characterSpawnPositions.Length];

            SetResultPlayerData(0, "Fighter", 1000, 500, 5, 5);
            SetResultPlayerData(1, "Fighter", 1100, 400, 4, 6);
            SetResultPlayerData(2, "Fighter", 1200, 300, 3, 7);
            SetResultPlayerData(3, "Fighter", 1300, 200, 2, 8);
            SetResultPlayerData(4, "Fighter", 1400, 100, 1, 9);
            SetResultPlayerData(5, "Fighter", 1500, 0, 0, 10);
        }

        private void SetResultPlayerData(
            int playerIndex,
            string roleType,
            int damage,
            int heal,
            int kill,
            int death)
        {
            if (playerIndex >= _resultPlayerDataTexts.Length ||
                playerIndex >= _characterSpawnPositions.Length)
            {
                Debug.LogWarning(
                    $"範囲外アクセス : {playerIndex}");

                return;
            }

            _resultPlayerDataTexts[playerIndex].text =
                $"{roleType}" +
                $"\nDAMAGE : {damage}" +
                $"\nHEAL   : {heal}" +
                $"\nKILL   : {kill}" +
                $"\nDEATH  : {death}";

            _playerObjects[playerIndex] =
                Instantiate(
                    _fighterPrefab,
                    _characterSpawnPositions[playerIndex].position,
                    _characterSpawnPositions[playerIndex].rotation);

            _playerObjects[playerIndex]
                .AddComponent<RotateObject>();
        }
    }
}
