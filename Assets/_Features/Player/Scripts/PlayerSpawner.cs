using UnityEngine;
using Uraty.Shared.Battle;

namespace Uraty.Features.Player
{
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private TeamId _teamId = TeamId.None;
        [SerializeField] private int _slotIndex;

        private GameObject _spawnedPlayer;

        public Transform SpawnPoint => _spawnPoint;
        public TeamId TeamId => _teamId;
        public int SlotIndex => _slotIndex;

        private void Start()
        {
            SpawnPlayer();
        }

        public void SpawnPlayer()
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("PlayerPrefab is not assigned");
                return;
            }

            if (_spawnPoint == null)
            {
                Debug.LogError("SpawnPoint is not assigned");
                return;
            }

            _spawnedPlayer = Instantiate(
                _playerPrefab,
                _spawnPoint.position,
                _spawnPoint.rotation
            );
        }
    }
}
