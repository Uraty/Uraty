using UnityEditor;
using UnityEngine;

namespace Uraty.Feature.MapEditor
{
    public sealed class StageEditorWindow : EditorWindow
    {
        private const float RightPanelWidthRatio = 0.34f;
        private const float MinRightPanelWidth = 320f;
        private const float MaxRightPanelWidth = 520f;
        private const float SplitSpacing = 6f;

        private StageData _stageData;
        private StagePreviewRenderer _previewRenderer;

        private Vector2 _rightPanelScrollPosition;
        private Vector2 _paletteScrollPosition;

        private int _selectedPrefabIndex;

        private int _editWidth = 10;
        private int _editHeight = 10;

        private int _lastPaintX = -1;
        private int _lastPaintZ = -1;

        private bool _showGrid = true;
        private bool _isPanningCamera;

        [MenuItem("Tools/Stage Editor")]
        private static void OpenFromMenu()
        {
            OpenWindow(null);
        }

        public static void OpenWindow(StageData stageData)
        {
            StageEditorWindow window = GetWindow<StageEditorWindow>("Stage Editor");
            window.SetStageData(stageData);
            window.Show();
        }

        private void SetStageData(StageData stageData)
        {
            _stageData = stageData;
            ClampSelectedPrefabIndex();

            if (_stageData != null)
            {
                SyncStageSizeFields();
            }

            if (_previewRenderer != null)
            {
                _previewRenderer.SetStageData(_stageData);
                _previewRenderer.SetShowGrid(_showGrid);
            }

            Repaint();
        }

        private void OnEnable()
        {
            minSize = new Vector2(960f, 540f);

            _previewRenderer = new StagePreviewRenderer();
            _previewRenderer.SetShowGrid(_showGrid);

            if (_stageData != null)
            {
                SyncStageSizeFields();
                _previewRenderer.SetStageData(_stageData);
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

        private void OnGUI()
        {
            float totalWidth = Mathf.Max(1f, position.width - 12f);
            float rightPanelWidth = Mathf.Clamp(totalWidth * RightPanelWidthRatio, MinRightPanelWidth, MaxRightPanelWidth);
            float leftPanelWidth = Mathf.Max(200f, totalWidth - rightPanelWidth - SplitSpacing);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewPane(leftPanelWidth);

                GUILayout.Space(SplitSpacing);

                DrawRightPane(rightPanelWidth);
            }
        }

        private void DrawPreviewPane(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                Rect previewRect = GUILayoutUtility.GetRect(
                    10f,
                    10000f,
                    10f,
                    10000f,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));

                GUI.Box(previewRect, GUIContent.none);

                if (_stageData == null)
                {
                    GUI.Label(previewRect, "Stage Data を設定しろ。", EditorStyles.centeredGreyMiniLabel);
                    return;
                }

                HandlePreviewInput(previewRect);

                if (Event.current.type == EventType.Repaint && _previewRenderer != null)
                {
                    Texture texture = _previewRenderer.Render(previewRect);
                    if (texture != null)
                    {
                        GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
                    }
                }

                DrawPreviewOverlay(previewRect);
            }
        }

        private void DrawRightPane(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                _rightPanelScrollPosition = EditorGUILayout.BeginScrollView(
                    _rightPanelScrollPosition,
                    GUILayout.ExpandHeight(true));

                DrawAssetField();
                EditorGUILayout.Space(8f);

                DrawStageControls();
                EditorGUILayout.Space(8f);

                DrawPalette();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAssetField()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("参照", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();

                StageData nextStageData = (StageData)EditorGUILayout.ObjectField(
                    "Stage Data",
                    _stageData,
                    typeof(StageData),
                    false);

                if (EditorGUI.EndChangeCheck())
                {
                    SetStageData(nextStageData);
                }
            }
        }

        private void DrawStageControls()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Stage Data", EditorStyles.boldLabel);

                if (_stageData == null)
                {
                    EditorGUILayout.HelpBox("Stage Data を設定しろ。", MessageType.Info);
                    return;
                }

                _editWidth = Mathf.Max(1, EditorGUILayout.IntField("Width", _editWidth));
                _editHeight = Mathf.Max(1, EditorGUILayout.IntField("Height", _editHeight));

                EditorGUI.BeginChangeCheck();
                bool nextShowGrid = EditorGUILayout.Toggle("Grid", _showGrid);
                if (EditorGUI.EndChangeCheck())
                {
                    _showGrid = nextShowGrid;

                    if (_previewRenderer != null)
                    {
                        _previewRenderer.SetShowGrid(_showGrid);
                    }

                    Repaint();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Resize"))
                    {
                        Undo.RecordObject(_stageData, "Resize Stage Data");
                        _stageData.Resize(_editWidth, _editHeight);
                        MarkStageDataDirtyAndRefresh();
                    }

                    if (GUILayout.Button("Clear All"))
                    {
                        bool confirmed = EditorUtility.DisplayDialog(
                            "Clear All",
                            "すべての配置を削除する。本当に実行するか？",
                            "削除する",
                            "キャンセル");

                        if (confirmed)
                        {
                            Undo.RecordObject(_stageData, "Clear Stage Data");
                            _stageData.ClearAll();
                            MarkStageDataDirtyAndRefresh();
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Save Assets"))
                    {
                        EditorUtility.SetDirty(_stageData);
                        AssetDatabase.SaveAssets();
                    }

                    if (GUILayout.Button("Fit Camera"))
                    {
                        if (_previewRenderer != null)
                        {
                            _previewRenderer.FrameStage();
                            Repaint();
                        }
                    }
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField($"現在サイズ: {_stageData.Width} x {_stageData.Height}");
                EditorGUILayout.LabelField($"配置セル数: {_stageData.Cells.Count}");
            }
        }

        private void DrawPalette()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

                if (_stageData == null)
                {
                    EditorGUILayout.HelpBox("Stage Data を設定しろ。", MessageType.Info);
                    return;
                }

                _paletteScrollPosition = EditorGUILayout.BeginScrollView(_paletteScrollPosition, GUILayout.Height(260f));

                if (_stageData.PaletteCount <= 0)
                {
                    EditorGUILayout.HelpBox("Palette が空だ。下のボタンから追加しろ。", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < _stageData.PaletteCount; i++)
                    {
                        DrawPaletteRow(i);
                    }
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Prefab スロットを追加"))
                {
                    Undo.RecordObject(_stageData, "Add Palette Prefab Slot");
                    _stageData.AddPalettePrefab();
                    ClampSelectedPrefabIndex();
                    MarkStageDataDirty();
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("左クリック: 配置");
                EditorGUILayout.LabelField("右クリック: 削除");
                EditorGUILayout.LabelField("中ドラッグ: カメラ移動");
                EditorGUILayout.LabelField("ホイール: ズーム");

                GameObject selectedPrefab = GetSelectedPrefab();
                string selectedText = selectedPrefab != null
                    ? $"現在選択: {selectedPrefab.name}"
                    : "現在選択: なし";

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(selectedText);
            }
        }

        private void DrawPaletteRow(int index)
        {
            GameObject prefab = _stageData.GetPalettePrefab(index);
            bool isSelected = index == _selectedPrefabIndex;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Color oldBackgroundColor = GUI.backgroundColor;
                    if (isSelected)
                    {
                        GUI.backgroundColor = new Color(0.35f, 0.8f, 1f, 1f);
                    }

                    if (GUILayout.Button(isSelected ? "選択中" : "選択", GUILayout.Width(60f)))
                    {
                        _selectedPrefabIndex = index;
                        GUI.FocusControl(null);
                    }

                    GUI.backgroundColor = oldBackgroundColor;

                    EditorGUILayout.LabelField($"[{index}]", GUILayout.Width(28f));

                    EditorGUI.BeginChangeCheck();
                    GameObject nextPrefab = (GameObject)EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_stageData, "Set Palette Prefab");
                        _stageData.SetPalettePrefab(index, nextPrefab);
                        MarkStageDataDirty();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(index <= 0))
                    {
                        if (GUILayout.Button("↑", GUILayout.Width(40f)))
                        {
                            Undo.RecordObject(_stageData, "Move Palette Prefab Up");
                            _stageData.MovePalettePrefab(index, index - 1);
                            OnPaletteMoved(index, index - 1);
                            MarkStageDataDirty();
                            GUIUtility.ExitGUI();
                        }
                    }

                    using (new EditorGUI.DisabledScope(index >= _stageData.PaletteCount - 1))
                    {
                        if (GUILayout.Button("↓", GUILayout.Width(40f)))
                        {
                            Undo.RecordObject(_stageData, "Move Palette Prefab Down");
                            _stageData.MovePalettePrefab(index, index + 1);
                            OnPaletteMoved(index, index + 1);
                            MarkStageDataDirty();
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (GUILayout.Button("削除"))
                    {
                        Undo.RecordObject(_stageData, "Remove Palette Prefab");
                        _stageData.RemovePalettePrefabAt(index);
                        OnPaletteRemoved(index);
                        MarkStageDataDirty();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        private void HandlePreviewInput(Rect previewRect)
        {
            if (_stageData == null || _previewRenderer == null)
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
                        }
                        return;
                }
            }

            if (!previewRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseUp)
                {
                    ResetLastPaintCell();
                }

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
                        return;
                    }

                    if (currentEvent.button == 0 || currentEvent.button == 1)
                    {
                        GUI.FocusControl(null);
                        ApplyBrush(currentEvent.mousePosition, previewRect, currentEvent.button == 1);
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (currentEvent.button == 0 || currentEvent.button == 1)
                    {
                        ApplyBrush(currentEvent.mousePosition, previewRect, currentEvent.button == 1);
                        currentEvent.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (currentEvent.button == 0 || currentEvent.button == 1)
                    {
                        ResetLastPaintCell();
                        currentEvent.Use();
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

        private void ApplyBrush(Vector2 mousePosition, Rect previewRect, bool shouldErase)
        {
            int x;
            int z;
            if (!_previewRenderer.TryGetCellAtGuiPosition(mousePosition, previewRect, out x, out z))
            {
                return;
            }

            if (_lastPaintX == x && _lastPaintZ == z)
            {
                return;
            }

            _lastPaintX = x;
            _lastPaintZ = z;

            if (shouldErase)
            {
                Undo.RecordObject(_stageData, "Erase Stage Block");
                _stageData.ClearPrefab(x, z);
                MarkStageDataDirtyAndRefresh();
                return;
            }

            GameObject selectedPrefab = GetSelectedPrefab();
            if (selectedPrefab == null)
            {
                return;
            }

            Undo.RecordObject(_stageData, "Paint Stage Block");
            _stageData.SetPrefab(x, z, selectedPrefab);
            MarkStageDataDirtyAndRefresh();
        }

        private void DrawPreviewOverlay(Rect previewRect)
        {
            if (_previewRenderer == null || _stageData == null)
            {
                return;
            }

            int hoverX;
            int hoverZ;
            bool hasHoverCell = _previewRenderer.TryGetCellAtGuiPosition(Event.current.mousePosition, previewRect, out hoverX, out hoverZ);

            string cellText = hasHoverCell ? $"Cell: ({hoverX}, {hoverZ})" : "Cell: -";

            GameObject selectedPrefab = GetSelectedPrefab();
            string prefabText = selectedPrefab != null
                ? $"Prefab: {selectedPrefab.name}"
                : "Prefab: None";

            Rect infoRect = new Rect(previewRect.x + 8f, previewRect.y + 8f, 420f, 72f);
            GUI.Box(infoRect, GUIContent.none);

            Rect labelRect = new Rect(infoRect.x + 8f, infoRect.y + 6f, infoRect.width - 16f, 18f);
            GUI.Label(labelRect, cellText);

            labelRect.y += 16f;
            GUI.Label(labelRect, prefabText);

            labelRect.y += 16f;
            GUI.Label(labelRect, "LMB: Paint / RMB: Erase / MMB Drag: Pan / Wheel: Zoom");
        }

        private GameObject GetSelectedPrefab()
        {
            if (_stageData == null || _stageData.PaletteCount <= 0)
            {
                return null;
            }

            _selectedPrefabIndex = Mathf.Clamp(_selectedPrefabIndex, 0, _stageData.PaletteCount - 1);
            return _stageData.GetPalettePrefab(_selectedPrefabIndex);
        }

        private void ClampSelectedPrefabIndex()
        {
            if (_stageData == null || _stageData.PaletteCount <= 0)
            {
                _selectedPrefabIndex = 0;
                return;
            }

            _selectedPrefabIndex = Mathf.Clamp(_selectedPrefabIndex, 0, _stageData.PaletteCount - 1);
        }

        private void OnPaletteMoved(int fromIndex, int toIndex)
        {
            if (_selectedPrefabIndex == fromIndex)
            {
                _selectedPrefabIndex = toIndex;
                return;
            }

            if (_selectedPrefabIndex == toIndex)
            {
                _selectedPrefabIndex = fromIndex;
            }
        }

        private void OnPaletteRemoved(int removedIndex)
        {
            if (_stageData == null || _stageData.PaletteCount <= 0)
            {
                _selectedPrefabIndex = 0;
                return;
            }

            if (_selectedPrefabIndex > removedIndex)
            {
                _selectedPrefabIndex--;
            }

            if (_selectedPrefabIndex >= _stageData.PaletteCount)
            {
                _selectedPrefabIndex = _stageData.PaletteCount - 1;
            }
        }

        private void SyncStageSizeFields()
        {
            if (_stageData == null)
            {
                _editWidth = 10;
                _editHeight = 10;
                return;
            }

            _editWidth = _stageData.Width;
            _editHeight = _stageData.Height;
        }

        private void MarkStageDataDirty()
        {
            EditorUtility.SetDirty(_stageData);
            Repaint();
        }

        private void MarkStageDataDirtyAndRefresh()
        {
            EditorUtility.SetDirty(_stageData);

            if (_previewRenderer != null)
            {
                _previewRenderer.Rebuild();
            }

            Repaint();
        }

        private void ResetLastPaintCell()
        {
            _lastPaintX = -1;
            _lastPaintZ = -1;
        }
    }
}
