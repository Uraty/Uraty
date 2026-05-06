using UnityEditor;
using System.Collections.Generic;


using UnityEngine;

namespace Uraty.Features.Stage
{
    [CustomEditor(typeof(StageData))]
    public sealed class StageDataEditor : Editor
    {
        private StagePreviewRenderer _previewRenderer;

        private bool _isPanningCamera;
        private int _lastPreviewHash = int.MinValue;

        private StageData TargetStageData => (StageData)target;

        private void OnEnable()
        {
            EnsurePreviewRenderer();
            _lastPreviewHash = ComputePreviewHash();

            if (_previewRenderer != null)
            {
                _previewRenderer.FrameStage();
            }
        }

        private void OnDisable()
        {
            _isPanningCamera = false;

            if (_previewRenderer != null)
            {
                _previewRenderer.Dispose();
                _previewRenderer = null;
            }
        }

        public override void OnInspectorGUI()
        {
            StageData stageData = TargetStageData;
            if (stageData == null)
            {
                return;
            }

            EnsurePreviewRenderer();
            RefreshPreviewIfNeeded();

            DrawStageInfo(stageData);
            DrawLargePreview();
        }

        public override bool RequiresConstantRepaint()
        {
            return _isPanningCamera;
        }

        private void DrawStageInfo(StageData stageData)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Stage Data", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("Width", stageData.Width);
                    EditorGUILayout.IntField("Height", stageData.Height);
                    EditorGUILayout.IntField("Palette Count", stageData.PaletteCount);
                    EditorGUILayout.IntField("Placed Cells", stageData.Cells.Count);
                }

                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Stage Editor"))
                    {
                        StageEditorWindow.OpenWindow(stageData);
                    }

                    if (GUILayout.Button("Save Asset"))
                    {
                        EditorUtility.SetDirty(stageData);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        private void DrawLargePreview()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Stage Preview", EditorStyles.boldLabel);

                float previewHeight = GetPreviewHeight();

                Rect previewRect = GUILayoutUtility.GetRect(
                    10f,
                    10000f,
                    previewHeight,
                    previewHeight,
                    GUILayout.ExpandWidth(true));

                GUI.Box(previewRect, GUIContent.none);

                HandlePreviewInput(previewRect);

                if (Event.current.type == EventType.Repaint && _previewRenderer != null)
                {
                    Texture texture = _previewRenderer.Render(previewRect);
                    if (texture != null)
                    {
                        GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
                    }
                }
            }
        }

        private float GetPreviewHeight()
        {
            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            float estimatedHeight = inspectorWidth * 0.9f;

            return Mathf.Clamp(estimatedHeight, 220f, 720f);
        }

        private void EnsurePreviewRenderer()
        {
            if (_previewRenderer != null)
            {
                return;
            }

            _previewRenderer = new StagePreviewRenderer();
            _previewRenderer.SetShowGrid(false);

            StageData stageData = TargetStageData;
            if (stageData != null)
            {
                _previewRenderer.SetStageData(stageData);
            }
        }

        private void RefreshPreviewIfNeeded()
        {
            StageData stageData = TargetStageData;
            if (stageData == null || _previewRenderer == null)
            {
                return;
            }

            _previewRenderer.SetStageData(stageData);
            _previewRenderer.SetShowGrid(false);

            int currentHash = ComputePreviewHash();
            if (currentHash != _lastPreviewHash)
            {
                _lastPreviewHash = currentHash;
                _previewRenderer.Rebuild();
            }
        }

        private int ComputePreviewHash()
        {
            StageData stageData = TargetStageData;
            if (stageData == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;

                hash = hash * 31 + stageData.Width;
                hash = hash * 31 + stageData.Height;
                hash = hash * 31 + stageData.PaletteCount;
                hash = hash * 31 + stageData.Cells.Count;

                IReadOnlyList<GameObject> palettePrefabs = stageData.PalettePrefabs;
                if (palettePrefabs != null)
                {
                    for (int i = 0; i < palettePrefabs.Count; i++)
                    {
                        GameObject prefab = palettePrefabs[i];
                        hash = hash * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
                    }
                }

                IReadOnlyList<StageCellData> cells = stageData.Cells;
                if (cells != null)
                {
                    for (int i = 0; i < cells.Count; i++)
                    {
                        StageCellData cell = cells[i];
                        hash = hash * 31 + cell.X;
                        hash = hash * 31 + cell.Z;
                        hash = hash * 31 + (cell.Prefab != null ? cell.Prefab.GetInstanceID() : 0);
                    }
                }

                return hash;
            }
        }

        private void HandlePreviewInput(Rect previewRect)
        {
            if (_previewRenderer == null)
            {
                return;
            }

            Event currentEvent = Event.current;

            if (_isPanningCamera)
            {
                switch (currentEvent.type)
                {
                    case EventType.MouseDrag:
                        if (currentEvent.button == 2)
                        {
                            Vector2 previousMousePosition = currentEvent.mousePosition - currentEvent.delta;
                            _previewRenderer.Pan(previousMousePosition, currentEvent.mousePosition, previewRect);
                            currentEvent.Use();
                            Repaint();
                        }
                        return;

                    case EventType.MouseUp:
                        if (currentEvent.button == 2)
                        {
                            _isPanningCamera = false;
                            currentEvent.Use();
                            Repaint();
                        }
                        return;
                }
            }

            if (!previewRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 2)
                    {
                        GUI.FocusControl(null);
                        _isPanningCamera = true;
                        currentEvent.Use();
                        Repaint();
                    }
                    break;

                case EventType.ScrollWheel:
                    _previewRenderer.Zoom(currentEvent.delta.y, currentEvent.mousePosition, previewRect);
                    currentEvent.Use();
                    Repaint();
                    break;

                case EventType.ContextClick:
                    currentEvent.Use();
                    break;
            }
        }
    }
}
