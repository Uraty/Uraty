using TMPro;
using UnityEngine;

namespace Uraty.Application.Result
{
    public sealed class PlayerScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _playerNameText;
        [SerializeField] private TMP_Text _killCountText;
        [SerializeField] private TMP_Text _deathCountText;
        [SerializeField] private TMP_Text _damageText;

        public void SetScore(PlayerScore score)
        {
            _playerNameText.text = score.PlayerName;
            _killCountText.text = score.KillCount.ToString();
            _deathCountText.text = score.DeathCount.ToString();
            _damageText.text = score.DamageDealt.ToString();
        }
    }
}
