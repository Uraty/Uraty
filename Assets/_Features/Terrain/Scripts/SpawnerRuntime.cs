using System.Collections.Generic;

using UnityEngine;

using Uraty.Shared.Battle;

namespace Uraty.Features.Terrain
{
    /// <summary>
    /// Spawner 地形の生成処理を担当する。
    ///
    /// このクラスは「一定間隔でオブジェクトを生成する」ことと、
    /// 「同時存在数の上限を守る」ことを担当する。
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

        // 現在シーン上に存在している生成物を追跡する。
        private readonly List<GameObject> _spawnedObjects = new();

        private int _slotIndex = -1;
        private float _elapsedSeconds;

        public TeamId TeamId => _teamId;
        public int SlotIndex => _slotIndex;
        public Transform SpawnPoint => _spawnPoint != null ? _spawnPoint : transform;

        /// <summary>
        /// Spawner にチームとスロット番号を割り当てる。
        /// </summary>
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

            // 既に破棄された生成物の参照を掃除して、
            // 現在の同時存在数を正しく保つ。
            CleanupDestroyedObjects();

            if (!CanSpawn())
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

        /// <summary>
        /// 現在の同時存在数上限に照らして生成可能かを返す。
        /// </summary>
        private bool CanSpawn()
        {
            if (_maxSpawnCount <= 0)
            {
                return true;
            }

            return _spawnedObjects.Count < _maxSpawnCount;
        }

        /// <summary>
        /// 既に破棄された生成物を追跡リストから除去する。
        /// </summary>
        private void CleanupDestroyedObjects()
        {
            _spawnedObjects.RemoveAll(spawnedObject => spawnedObject == null);
        }

        /// <summary>
        /// 生成物を1つ生成し、追跡対象に追加する。
        /// </summary>
        private void Spawn()
        {
            if (!CanSpawn())
            {
                return;
            }

            GameObject spawnedObject = Instantiate(
                _spawnPrefab,
                SpawnPoint.position,
                SpawnPoint.rotation);

            _spawnedObjects.Add(spawnedObject);
        }
    }
}
