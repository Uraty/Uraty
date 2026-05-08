using System;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace Uraty.Application.Stage
{
    public sealed class StagePreviewRenderer : IDisposable
    {
        private const float CameraPitchDegrees = 55f;
        private const float DefaultOrthographicSize = 6f;
        private const float ZoomSensitivity = 0.08f;

        private const float GridLinePixelWidth = 1.5f;
        private const float MinGridLineThickness = 0.03f;
        private const float MaxGridLineThickness = 0.18f;
        private const float GridLineHeight = 0.03f;
        private const float GridLineY = 0.015f;

        private StageData _stageData;
        private PreviewRenderUtility _previewUtility;

        private GameObject _stageRoot;
        private GameObject _staticRoot;
        private GameObject _cellRoot;

        private GameObject _floorObject;
        private GameObject _gridRoot;

        private Material _floorMaterial;
        private Material _gridMaterial;

        private readonly Dictionary<Vector2Int, GameObject> _cellObjects = new();

        private Vector3 _pivot = new(5f, 0f, 5f);
        private float _orthographicSize = DefaultOrthographicSize;

        private bool _showGrid = true;

        public void SetStageData(StageData stageData)
        {
            if (_stageData == stageData)
            {
                return;
            }

            _stageData = stageData;
            Rebuild();

            if (_stageData != null)
            {
                FrameStage();
            }
        }

        public void SetShowGrid(bool showGrid)
        {
            if (_showGrid == showGrid)
            {
                return;
            }

            _showGrid = showGrid;
            RebuildStatic();
        }

        public void Rebuild()
        {
            EnsurePreviewUtility();

            ClearStaticObjects();
            ClearAllCellObjects();

            if (_stageData != null)
            {
                BuildFloor();

                if (_showGrid)
                {
                    BuildGrid();
                }

                BuildAllCellObjects();
            }

            ClampCameraState();
            ApplyCameraTransform();
            SetupLights();
        }

        public void RebuildStatic()
        {
            EnsurePreviewUtility();

            ClearStaticObjects();

            if (_stageData != null)
            {
                BuildFloor();

                if (_showGrid)
                {
                    BuildGrid();
                }
            }

            ClampCameraState();
            ApplyCameraTransform();
            SetupLights();
        }

        public void UpdateCell(int x, int z)
        {
            EnsurePreviewUtility();

            if (_stageData == null)
            {
                return;
            }

            if (!_stageData.IsInBounds(x, z))
            {
                return;
            }

            Vector2Int key = new(x, z);
            RemoveCellObject(key);

            if (!_stageData.TryGetPrefab(x, z, out GameObject prefab) || prefab == null)
            {
                return;
            }

            CreateCellObject(x, z, prefab);
        }

        public void FrameStage()
        {
            EnsurePreviewUtility();

            float width = _stageData != null ? Mathf.Max(1, _stageData.Width) : 10f;
            float height = _stageData != null ? Mathf.Max(1, _stageData.Height) : 10f;

            _pivot = new Vector3(width * 0.5f, 0f, height * 0.5f);

            float size = Mathf.Max(width, height);
            _orthographicSize = Mathf.Clamp(
                Mathf.Max(3f, size * 0.65f),
                GetMinOrthographicSize(),
                GetMaxOrthographicSize());

            ClampCameraState();
            ApplyCameraTransform();
            SetupLights();
        }

        public void Pan(Vector2 previousGuiPosition, Vector2 currentGuiPosition, Rect previewRect)
        {
            EnsurePreviewUtility();

            if (_stageData == null)
            {
                return;
            }

            if (!TryGetGroundPointAtGuiPosition(previousGuiPosition, previewRect, out Vector3 previousGroundPoint))
            {
                return;
            }

            if (!TryGetGroundPointAtGuiPosition(currentGuiPosition, previewRect, out Vector3 currentGroundPoint))
            {
                return;
            }

            Vector3 delta = previousGroundPoint - currentGroundPoint;
            delta.y = 0f;

            _pivot += delta;

            ClampCameraState();
            ApplyCameraTransform();
        }

        public void Zoom(float scrollDelta, Vector2 guiPosition, Rect previewRect)
        {
            EnsurePreviewUtility();

            if (_stageData == null)
            {
                return;
            }

            bool hasBeforePoint = TryGetGroundPointAtGuiPosition(guiPosition, previewRect, out Vector3 beforePoint);

            float zoomFactor = 1f + scrollDelta * ZoomSensitivity;
            zoomFactor = Mathf.Max(0.1f, zoomFactor);

            _orthographicSize = Mathf.Clamp(
                _orthographicSize * zoomFactor,
                GetMinOrthographicSize(),
                GetMaxOrthographicSize());

            ClampCameraState();
            ApplyCameraTransform();

            if (hasBeforePoint && TryGetGroundPointAtGuiPosition(guiPosition, previewRect, out Vector3 afterPoint))
            {
                Vector3 offset = beforePoint - afterPoint;
                offset.y = 0f;
                _pivot += offset;

                ClampCameraState();
                ApplyCameraTransform();
            }
        }

        public Texture Render(Rect rect)
        {
            EnsurePreviewUtility();
            UpdateCameraAspect(rect);
            ApplyCameraTransform();
            UpdateGridLineThickness(rect);

            _previewUtility.BeginPreview(rect, GUIStyle.none);

            Camera camera = _previewUtility.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            camera.Render();

            return _previewUtility.EndPreview();
        }

        public bool TryGetCellAtGuiPosition(Vector2 guiPosition, Rect previewRect, out int x, out int z)
        {
            x = -1;
            z = -1;

            if (_stageData == null || _previewUtility == null)
            {
                return false;
            }

            if (!TryGetGroundPointAtGuiPosition(guiPosition, previewRect, out Vector3 hitPoint))
            {
                return false;
            }

            if (hitPoint.x < 0f || hitPoint.z < 0f)
            {
                return false;
            }

            if (hitPoint.x >= _stageData.Width || hitPoint.z >= _stageData.Height)
            {
                return false;
            }

            x = Mathf.FloorToInt(hitPoint.x);
            z = Mathf.FloorToInt(hitPoint.z);

            return _stageData.IsInBounds(x, z);
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void EnsurePreviewUtility()
        {
            if (_previewUtility != null)
            {
                return;
            }

            _previewUtility = new PreviewRenderUtility();
            _previewUtility.camera.cameraType = CameraType.Preview;
            _previewUtility.camera.allowHDR = false;
            _previewUtility.camera.allowMSAA = true;
            _previewUtility.camera.orthographic = true;

            _stageRoot = new GameObject("StagePreviewRoot");
            _stageRoot.hideFlags = HideFlags.HideAndDontSave;

            _staticRoot = new GameObject("StaticRoot");
            _staticRoot.hideFlags = HideFlags.HideAndDontSave;
            _staticRoot.transform.SetParent(_stageRoot.transform, false);

            _cellRoot = new GameObject("CellRoot");
            _cellRoot.hideFlags = HideFlags.HideAndDontSave;
            _cellRoot.transform.SetParent(_stageRoot.transform, false);

            _previewUtility.AddSingleGO(_stageRoot);
        }

        private void UpdateCameraAspect(Rect rect)
        {
            if (_previewUtility == null)
            {
                return;
            }

            float width = Mathf.Max(1f, rect.width);
            float height = Mathf.Max(1f, rect.height);
            _previewUtility.camera.aspect = width / height;
        }

        private void ApplyCameraTransform()
        {
            if (_previewUtility == null)
            {
                return;
            }

            float width = _stageData != null ? Mathf.Max(1, _stageData.Width) : 10f;
            float height = _stageData != null ? Mathf.Max(1, _stageData.Height) : 10f;
            float stageSize = Mathf.Max(width, height);

            Camera camera = _previewUtility.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1f, _orthographicSize);

            float cameraHeight = Mathf.Max(10f, stageSize + 10f);

            Quaternion rotation = Quaternion.Euler(CameraPitchDegrees, 0f, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 forwardOnGround = Vector3.ProjectOnPlane(forward, Vector3.up);

            float forwardOnGroundLength = forwardOnGround.magnitude;
            Vector3 forwardOnGroundDirection = forwardOnGroundLength > 0.0001f
                ? forwardOnGround / forwardOnGroundLength
                : Vector3.forward;

            float groundOffset = cameraHeight * forwardOnGroundLength / Mathf.Max(0.0001f, -forward.y);

            Vector3 cameraPosition =
                _pivot
                - forwardOnGroundDirection * groundOffset
                + Vector3.up * cameraHeight;

            camera.transform.position = cameraPosition;
            camera.transform.rotation = rotation;

            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(200f, cameraHeight + 100f);
        }

        private void ClampCameraState()
        {
            _orthographicSize = Mathf.Clamp(
                _orthographicSize,
                GetMinOrthographicSize(),
                GetMaxOrthographicSize());

            ClampPivot();
        }

        private void ClampPivot()
        {
            float width = _stageData != null ? Mathf.Max(1, _stageData.Width) : 10f;
            float height = _stageData != null ? Mathf.Max(1, _stageData.Height) : 10f;
            float margin = Mathf.Max(2f, Mathf.Max(width, height) * 0.5f);

            _pivot.x = Mathf.Clamp(_pivot.x, -margin, width + margin);
            _pivot.z = Mathf.Clamp(_pivot.z, -margin, height + margin);
        }

        private float GetMinOrthographicSize()
        {
            float width = _stageData != null ? Mathf.Max(1, _stageData.Width) : 10f;
            float height = _stageData != null ? Mathf.Max(1, _stageData.Height) : 10f;
            float size = Mathf.Max(width, height);
            return Mathf.Max(1.5f, size * 0.15f);
        }

        private float GetMaxOrthographicSize()
        {
            float width = _stageData != null ? Mathf.Max(1, _stageData.Width) : 10f;
            float height = _stageData != null ? Mathf.Max(1, _stageData.Height) : 10f;
            float size = Mathf.Max(width, height);
            return Mathf.Max(8f, size * 2.5f);
        }

        private bool TryGetGroundPointAtGuiPosition(Vector2 guiPosition, Rect previewRect, out Vector3 hitPoint)
        {
            hitPoint = default;

            if (_stageData == null || _previewUtility == null)
            {
                return false;
            }

            if (!previewRect.Contains(guiPosition))
            {
                return false;
            }

            UpdateCameraAspect(previewRect);
            ApplyCameraTransform();

            float u = Mathf.InverseLerp(previewRect.xMin, previewRect.xMax, guiPosition.x);
            float v = Mathf.InverseLerp(previewRect.yMin, previewRect.yMax, guiPosition.y);

            Ray ray = _previewUtility.camera.ViewportPointToRay(new Vector3(u, 1f - v, 0f));
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            hitPoint = ray.GetPoint(enter);
            return true;
        }

        private void SetupLights()
        {
            if (_previewUtility == null)
            {
                return;
            }

            Light keyLight = _previewUtility.lights[0];
            keyLight.intensity = 1.2f;
            keyLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light fillLight = _previewUtility.lights[1];
            fillLight.intensity = 1.0f;
            fillLight.transform.rotation = Quaternion.Euler(340f, 218f, 177f);
        }

        private void BuildFloor()
        {
            if (_stageData == null || _staticRoot == null)
            {
                return;
            }

            _floorObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floorObject.name = "StagePreviewFloor";
            _floorObject.hideFlags = HideFlags.HideAndDontSave;
            _floorObject.transform.SetParent(_staticRoot.transform, false);
            _floorObject.transform.localPosition = new Vector3(_stageData.Width * 0.5f, -0.01f, _stageData.Height * 0.5f);
            _floorObject.transform.localScale = new Vector3(_stageData.Width / 10f, 1f, _stageData.Height / 10f);

            Collider collider = _floorObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer renderer = _floorObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                _floorMaterial = CreateEditorMaterial(new Color(0.72f, 0.72f, 0.72f, 1f));
                renderer.sharedMaterial = _floorMaterial;
            }
        }

        private void BuildGrid()
        {
            if (_stageData == null || _staticRoot == null)
            {
                return;
            }

            _gridRoot = new GameObject("GridRoot");
            _gridRoot.hideFlags = HideFlags.HideAndDontSave;
            _gridRoot.transform.SetParent(_staticRoot.transform, false);

            _gridMaterial = CreateEditorMaterial(new Color(0.12f, 0.12f, 0.12f, 1f));

            for (int x = 0; x <= _stageData.Width; x++)
            {
                GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"Grid_V_{x}";
                line.hideFlags = HideFlags.HideAndDontSave;
                line.transform.SetParent(_gridRoot.transform, false);
                line.transform.localPosition = new Vector3(x, GridLineY, _stageData.Height * 0.5f);
                line.transform.localScale = new Vector3(MinGridLineThickness, GridLineHeight, _stageData.Height);

                Collider collider = line.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                Renderer renderer = line.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _gridMaterial;
                }
            }

            for (int z = 0; z <= _stageData.Height; z++)
            {
                GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"Grid_H_{z}";
                line.hideFlags = HideFlags.HideAndDontSave;
                line.transform.SetParent(_gridRoot.transform, false);
                line.transform.localPosition = new Vector3(_stageData.Width * 0.5f, GridLineY, z);
                line.transform.localScale = new Vector3(_stageData.Width, GridLineHeight, MinGridLineThickness);

                Collider collider = line.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                Renderer renderer = line.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _gridMaterial;
                }
            }
        }

        private void UpdateGridLineThickness(Rect previewRect)
        {
            if (_gridRoot == null || _stageData == null)
            {
                return;
            }

            float previewHeight = Mathf.Max(1f, previewRect.height);
            float worldUnitsPerPixel = (_orthographicSize * 2f) / previewHeight;

            float thickness = Mathf.Clamp(
                worldUnitsPerPixel * GridLinePixelWidth,
                MinGridLineThickness,
                MaxGridLineThickness);

            for (int i = 0; i < _gridRoot.transform.childCount; i++)
            {
                Transform line = _gridRoot.transform.GetChild(i);
                if (line == null)
                {
                    continue;
                }

                Vector3 scale = line.localScale;

                if (line.name.StartsWith("Grid_V_"))
                {
                    scale.x = thickness;
                    scale.y = GridLineHeight;
                    scale.z = _stageData.Height;
                }
                else if (line.name.StartsWith("Grid_H_"))
                {
                    scale.x = _stageData.Width;
                    scale.y = GridLineHeight;
                    scale.z = thickness;
                }

                line.localScale = scale;
            }
        }

        private void BuildAllCellObjects()
        {
            if (_stageData == null)
            {
                return;
            }

            IReadOnlyList<StageCellData> cells = _stageData.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                StageCellData cell = cells[i];
                if (cell.Prefab == null)
                {
                    continue;
                }

                CreateCellObject(cell.X, cell.Z, cell.Prefab);
            }
        }

        private void CreateCellObject(int x, int z, GameObject prefab)
        {
            if (_cellRoot == null || prefab == null)
            {
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }

            if (instance == null)
            {
                return;
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.name = $"Cell_{x}_{z}_{prefab.name}";
            instance.transform.SetParent(_cellRoot.transform, false);
            instance.transform.localRotation = Quaternion.identity;

            float yOffset = CalculateBottomAlignYOffset(instance);

            instance.transform.localPosition = new Vector3(
                x + 0.5f,
                yOffset + 0.02f,
                z + 0.5f);

            Vector2Int key = new(x, z);
            _cellObjects[key] = instance;
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
            return -bottomY;
        }

        private void RemoveCellObject(Vector2Int key)
        {
            if (_cellObjects.TryGetValue(key, out GameObject obj))
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }

                _cellObjects.Remove(key);
            }
        }

        private void ClearAllCellObjects()
        {
            foreach (KeyValuePair<Vector2Int, GameObject> pair in _cellObjects)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.DestroyImmediate(pair.Value);
                }
            }

            _cellObjects.Clear();
        }

        private void ClearStaticObjects()
        {
            if (_floorObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_floorObject);
                _floorObject = null;
            }

            if (_gridRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_gridRoot);
                _gridRoot = null;
            }

            if (_floorMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(_floorMaterial);
                _floorMaterial = null;
            }

            if (_gridMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(_gridMaterial);
                _gridMaterial = null;
            }
        }

        private Material CreateEditorMaterial(Color color)
        {
            Material baseMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            Material material = baseMaterial != null
                ? new Material(baseMaterial)
                : new Material(Shader.Find("Standard"));

            material.color = color;
            return material;
        }

        private void Cleanup()
        {
            ClearStaticObjects();
            ClearAllCellObjects();

            if (_stageRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_stageRoot);
                _stageRoot = null;
            }

            _staticRoot = null;
            _cellRoot = null;

            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }
    }
}
