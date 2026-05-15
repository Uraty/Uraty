using System.Collections.Generic;

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Uraty.Application.Stage
{
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    public sealed class StageBuilder : MonoBehaviour
    {
        [SerializeField] private StageData _stageData;
        [SerializeField] private Transform _generatedRoot;
        [SerializeField] private bool _keepPrefabConnectionInEditor = true;
        [SerializeField] private StageGenerator _generator;

        public StageData StageData => _stageData;

#if UNITY_EDITOR
        private bool _isGenerateQueued;
#endif

        private void Start()
        {
            if (UnityEngine.Application.isPlaying)
            {
                Generate();
                _generator.GenerateStage();
            }
        }

        [ContextMenu("Generate Stage")]
        public void Generate()
        {
            if (_stageData == null)
            {
                Debug.LogError("StageData が設定されていない。", this);
                return;
            }

            Transform root = EnsureGeneratedRoot();

            Clear();
            root = EnsureGeneratedRoot();

            IReadOnlyList<StageCellData> cells = _stageData.Cells;
            if (cells == null || cells.Count == 0)
            {
                Debug.LogWarning("StageData に配置済みセルがない。何も生成されない。", this);
                return;
            }

            int generatedCount = 0;

            for (int i = 0; i < cells.Count; i++)
            {
                StageCellData cell = cells[i];
                if (cell.Prefab == null)
                {
                    continue;
                }

                GameObject instance = InstantiateStageObject(cell.Prefab, root);
                if (instance == null)
                {
                    Debug.LogWarning($"セル ({cell.X}, {cell.Z}) の Prefab 生成に失敗した: {cell.Prefab.name}", this);
                    continue;
                }

                instance.name = $"{cell.X}_{cell.Z}_{cell.Prefab.name}";
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                Vector3 localPosition = new Vector3(cell.X + 0.5f, 0f, cell.Z + 0.5f);

                instance.transform.localPosition = localPosition;
                generatedCount++;
            }

            Debug.Log($"StageBuilder: {generatedCount} 個生成した。", this);
        }

        [ContextMenu("Clear Stage")]
        public void Clear()
        {
            if (_generatedRoot == null)
            {
                return;
            }

            List<GameObject> children = new();

            for (int i = 0; i < _generatedRoot.childCount; i++)
            {
                Transform child = _generatedRoot.GetChild(i);
                if (child != null)
                {
                    children.Add(child.gameObject);
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                DestroyObject(children[i]);
            }
        }

        private Transform EnsureGeneratedRoot()
        {
            if (_generatedRoot != null)
            {
                return _generatedRoot;
            }

            Transform child = transform.Find("GeneratedStage");
            if (child != null)
            {
                _generatedRoot = child;
                return _generatedRoot;
            }

            GameObject rootObject = new GameObject("GeneratedStage");
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;
            return _generatedRoot;
        }

        private GameObject InstantiateStageObject(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying && _keepPrefabConnectionInEditor)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                if (instance != null)
                {
                    return instance;
                }
            }
#endif

            return Instantiate(prefab, parent);
        }

        private float CalculateBottomAlignYOffset(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                return 0f;
            }

            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return 0f;
            }

            float bottomY = combinedBounds.min.y;
            float rootY = instance.transform.position.y;
            return rootY - bottomY;
        }

        private void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif

            Destroy(target);
        }
    }
}
