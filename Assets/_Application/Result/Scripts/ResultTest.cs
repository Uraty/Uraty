using UnityEngine;

namespace Uraty.Application.Result
{
    public sealed class ResultTest : MonoBehaviour
    {
        private PlayerScore[] _score;
        ResultViewController _controller;

        private void Start()
        {
            _controller = GetComponent<ResultViewController>();
            _score = new PlayerScore[]
            {
                new PlayerScore("Player1", 1, 2, 3),
                new PlayerScore("Player2", 4, 5, 6),
                new PlayerScore("Player3", 7, 8, 9),
                new PlayerScore("Player4", 10, 11, 12),
                new PlayerScore("Player5", 13, 14, 15),
                new PlayerScore("Player6", 16, 17, 1300)

            };
            _controller.SetScores(_score);
        }


    }
}
