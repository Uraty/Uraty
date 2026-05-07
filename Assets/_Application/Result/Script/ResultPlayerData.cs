using UnityEngine;
using UnityEngine.UI;

namespace Uraty.Features.Result
{
    public class ResultPlayerData : MonoBehaviour
    {
        [Header("テキスト")]
        [SerializeField] private Text[] resultPlayerDataTexts;

        [Header("キャラクター")]
        [SerializeField] private GameObject Assassin;
        [SerializeField] private GameObject Attacker;
        [SerializeField] private GameObject Fighter;
        [SerializeField] private GameObject Sniper;

        [Header("キャラクター生成位置")]
        [SerializeField] private Transform[] CharacterSpawnPos;

        private GameObject[] playerObject;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            playerObject = new GameObject[CharacterSpawnPos.Length];

            SetResultPlayerData(0, "Fighter",
                1000, 500, 5, 5);
            SetResultPlayerData(1, "Fighter",
                1100, 400, 4, 6);
            SetResultPlayerData(2, "Fighter",
                1200, 300, 3, 7);
            SetResultPlayerData(3, "Fighter",
                1300, 200, 2, 8);
            SetResultPlayerData(4, "Fighter",
                1400, 100, 1, 9);
            SetResultPlayerData(5, "Fighter",
                1500, 0, 0, 10);
        }

        private void SetResultPlayerData(int playerIndex, string RoleType,
            int damage, int heel, int kill, int death)
        {
            resultPlayerDataTexts[playerIndex].text =
                RoleType +
                "\nDAMAGE : " + damage +
                "\nHEEL   : " + heel +
                "\nKILL   : " + kill +
                "\nDEATH  : " + death;

            playerObject[playerIndex] =
                Instantiate(
                    Fighter,
                    CharacterSpawnPos[playerIndex].position,
                    CharacterSpawnPos[playerIndex].rotation);

            playerObject[playerIndex].AddComponent<RotateObject>();
        }
    }
}
