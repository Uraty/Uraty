using System;
using System.Collections.Generic;

using UnityEngine;

namespace Uraty.Application.Stage
{
    [Serializable]
    public struct StageCellData
    {
        [Min(0)] public int X;
        [Min(0)] public int Z;
        public GameObject Prefab;

        public StageCellData(int x, int z, GameObject prefab)
        {
            X = x;
            Z = z;
            Prefab = prefab;
        }
    }

    [CreateAssetMenu(menuName = "Stage/Stage Data", fileName = "StageData")]
    public sealed class StageData : ScriptableObject
    {
        [SerializeField, HideInInspector] private GameObject[] _palettePrefabs = Array.Empty<GameObject>();

        [SerializeField, HideInInspector, Min(1)] private int _width = 10;
        [SerializeField, HideInInspector, Min(1)] private int _height = 10;
        [SerializeField, HideInInspector] private List<StageCellData> _cells = new();

        public int Width => _width;
        public int Height => _height;
        public IReadOnlyList<StageCellData> Cells => _cells;

        public IReadOnlyList<GameObject> PalettePrefabs => _palettePrefabs;
        public int PaletteCount => _palettePrefabs == null ? 0 : _palettePrefabs.Length;

        public void Initialize(int width, int height)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            RemoveInvalidCells();
        }

        public void Resize(int width, int height)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            RemoveInvalidCells();
        }

        public bool IsInBounds(int x, int z)
        {
            return x >= 0 && x < _width && z >= 0 && z < _height;
        }

        public GameObject GetPalettePrefab(int index)
        {
            if (_palettePrefabs == null)
            {
                return null;
            }

            if (index < 0 || index >= _palettePrefabs.Length)
            {
                return null;
            }

            return _palettePrefabs[index];
        }

        public void AddPalettePrefab(GameObject prefab = null)
        {
            int count = PaletteCount;
            Array.Resize(ref _palettePrefabs, count + 1);
            _palettePrefabs[count] = prefab;
        }

        public void SetPalettePrefab(int index, GameObject prefab)
        {
            if (_palettePrefabs == null)
            {
                return;
            }

            if (index < 0 || index >= _palettePrefabs.Length)
            {
                return;
            }

            _palettePrefabs[index] = prefab;
        }

        public void RemovePalettePrefabAt(int index)
        {
            if (_palettePrefabs == null)
            {
                return;
            }

            if (index < 0 || index >= _palettePrefabs.Length)
            {
                return;
            }

            if (_palettePrefabs.Length == 1)
            {
                _palettePrefabs = Array.Empty<GameObject>();
                return;
            }

            GameObject[] next = new GameObject[_palettePrefabs.Length - 1];

            if (index > 0)
            {
                Array.Copy(_palettePrefabs, 0, next, 0, index);
            }

            if (index < _palettePrefabs.Length - 1)
            {
                Array.Copy(_palettePrefabs, index + 1, next, index, _palettePrefabs.Length - index - 1);
            }

            _palettePrefabs = next;
        }

        public void MovePalettePrefab(int fromIndex, int toIndex)
        {
            if (_palettePrefabs == null)
            {
                return;
            }

            if (fromIndex < 0 || fromIndex >= _palettePrefabs.Length)
            {
                return;
            }

            if (toIndex < 0 || toIndex >= _palettePrefabs.Length)
            {
                return;
            }

            if (fromIndex == toIndex)
            {
                return;
            }

            GameObject movingPrefab = _palettePrefabs[fromIndex];

            if (fromIndex < toIndex)
            {
                for (int i = fromIndex; i < toIndex; i++)
                {
                    _palettePrefabs[i] = _palettePrefabs[i + 1];
                }
            }
            else
            {
                for (int i = fromIndex; i > toIndex; i--)
                {
                    _palettePrefabs[i] = _palettePrefabs[i - 1];
                }
            }

            _palettePrefabs[toIndex] = movingPrefab;
        }

        public bool ContainsPalettePrefab(GameObject prefab)
        {
            if (prefab == null || _palettePrefabs == null)
            {
                return false;
            }

            for (int i = 0; i < _palettePrefabs.Length; i++)
            {
                if (_palettePrefabs[i] == prefab)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetPrefab(int x, int z, out GameObject prefab)
        {
            prefab = null;

            if (!IsInBounds(x, z))
            {
                return false;
            }

            int index = FindCellIndex(x, z);
            if (index < 0)
            {
                return false;
            }

            prefab = _cells[index].Prefab;
            return prefab != null;
        }

        public GameObject GetPrefab(int x, int z)
        {
            if (!IsInBounds(x, z))
            {
                return null;
            }

            int index = FindCellIndex(x, z);
            if (index < 0)
            {
                return null;
            }

            return _cells[index].Prefab;
        }

        public void SetPrefab(int x, int z, GameObject prefab)
        {
            if (!IsInBounds(x, z))
            {
                Debug.LogError($"セル座標 ({x}, {z}) は範囲外です。サイズ: {_width} x {_height}", this);
                return;
            }

            if (prefab == null)
            {
                ClearPrefab(x, z);
                return;
            }

            int index = FindCellIndex(x, z);
            if (index >= 0)
            {
                _cells[index] = new StageCellData(x, z, prefab);
                return;
            }

            _cells.Add(new StageCellData(x, z, prefab));
        }

        public void ClearPrefab(int x, int z)
        {
            if (!IsInBounds(x, z))
            {
                Debug.LogError($"セル座標 ({x}, {z}) は範囲外です。サイズ: {_width} x {_height}", this);
                return;
            }

            int index = FindCellIndex(x, z);
            if (index >= 0)
            {
                _cells.RemoveAt(index);
            }
        }

        public void ClearAll()
        {
            _cells.Clear();
        }

        private int FindCellIndex(int x, int z)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                StageCellData cell = _cells[i];
                if (cell.X == x && cell.Z == z)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveInvalidCells()
        {
            HashSet<Vector2Int> usedPositions = new();

            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                StageCellData cell = _cells[i];

                if (!IsInBounds(cell.X, cell.Z))
                {
                    _cells.RemoveAt(i);
                    continue;
                }

                if (cell.Prefab == null)
                {
                    _cells.RemoveAt(i);
                    continue;
                }

                Vector2Int position = new(cell.X, cell.Z);
                if (!usedPositions.Add(position))
                {
                    _cells.RemoveAt(i);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _width = Mathf.Max(1, _width);
            _height = Mathf.Max(1, _height);

            if (_palettePrefabs == null)
            {
                _palettePrefabs = Array.Empty<GameObject>();
            }

            if (_cells == null)
            {
                _cells = new List<StageCellData>();
            }

            RemoveInvalidCells();
        }
#endif
    }
}
