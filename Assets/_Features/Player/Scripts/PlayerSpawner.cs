using UnityEngine;

namespace Uraty.Feature.Player
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform _spawnPoint;

        private GameObject _spawnedPlayer;

        public Transform SpawnPoint => _spawnPoint;

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
