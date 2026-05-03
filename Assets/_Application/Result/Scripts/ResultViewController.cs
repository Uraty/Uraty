using System.Collections.Generic;
using UnityEngine;

namespace Uraty.Application.Result
{
    public sealed class ResultViewController : MonoBehaviour
    {
        [SerializeField] private PlayerScoreView[] _playerViews;

        public void SetScores(IReadOnlyList<PlayerScore> scores)
        {
            if (scores == null)
            {
                Debug.LogError("Scores is null.");
                return;
            }

            if (_playerViews == null || _playerViews.Length < scores.Count)
            {
                Debug.LogError("PlayerViews is not enough.");
                return;
            }

            for (int i = 0; i < scores.Count; i++)
            {
                _playerViews[i].SetScore(scores[i]);
            }
        }
    }
}
