using UnityEngine;
using Uraty.Shared.Battle;

namespace Uraty.Feature.Terrain
{
    /// <summary>
    /// Spawner 地形の生成処理を担当する。
    /// </summary>
    public sealed class SpawnerRuntime : MonoBehaviour
    {
        [Header("生成する Prefab")]
        [SerializeField] private GameObject _spawnPrefab;

        [Header("生成間隔（秒）")]
        [SerializeField] private float _spawnIntervalSeconds = 5.0f;

        [Header("同時存在上限")]
        [SerializeField] private int _maxSpawnCount = 3;

        [Header("生成位置。未設定時は自分自身を使う")]
        [SerializeField] private Transform _spawnPoint;

        [Header("チーム情報")]
        [SerializeField] private TeamId _teamId = TeamId.None;

        private int _slotIndex = -1;
        private float _elapsedSeconds;
        private int _currentSpawnCount;

        public TeamId TeamId => _teamId;
        public int SlotIndex => _slotIndex;
        public Transform SpawnPoint => _spawnPoint != null ? _spawnPoint : transform;

        public void AssignTeam(TeamId teamId, int slotIndex)
        {
            _teamId = teamId;
            _slotIndex = slotIndex;
        }

        private void Awake()
        {
            if (_spawnIntervalSeconds < 0.0f)
            {
                _spawnIntervalSeconds = 0.0f;
            }

            if (_maxSpawnCount < 0)
            {
                _maxSpawnCount = 0;
            }
        }

        private void Update()
        {
            if (_spawnPrefab == null)
            {
                return;
            }

            if (_teamId == TeamId.None)
            {
                return;
            }

            if (_maxSpawnCount > 0 && _currentSpawnCount >= _maxSpawnCount)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            if (_elapsedSeconds < _spawnIntervalSeconds)
            {
                return;
            }

            _elapsedSeconds = 0.0f;
            Spawn();
        }

        private void Spawn()
        {
            Instantiate(
                _spawnPrefab,
                SpawnPoint.position,
                SpawnPoint.rotation);

            _currentSpawnCount++;
        }
    }
}
