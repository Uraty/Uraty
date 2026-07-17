using UnityEngine;

using Uraty.Systems.Sound;

namespace Uraty.Application.Matching
{
    public class MatcingSoundManager : MonoBehaviour
    {
        [SerializeField]private SoundManager _soundManager;

        [SerializeField]private AudioClip _matchingSe;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _soundManager.PlaySE(_matchingSe);
        }
    }
}
