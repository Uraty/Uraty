using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Uraty.Feature.Akane_TestCharacter;
using Uraty.Features.Character;
using Uraty.Systems.Input;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// キャラ選択画面を管理するクラス。
    /// 3Dキャラモデルを横並びで表示し、中央のキャラを選択中として扱う。
    /// </summary>
    public sealed class CharacterSelectSceneController : MonoBehaviour
    {
        [Header("Input")]
        // 入力管理クラス。
        // 未設定の場合はStartでHierarchy上から探す。
        [SerializeField] private GameInput _gameInput;

        [Header("Store")]
        // ロビーとキャラ選択画面で共有する選択キャラStore。
        [SerializeField] private CharacterSelectionStore _characterSelectionStore;

        [Header("Characters")]
        // キャラ選択画面に表示するキャラデータ一覧。
        [SerializeField] private GameObject[] _characterPrefabs;

        [Header("Preview")]
        // 3Dキャラモデルを横並びで生成する親。
        [SerializeField] private Transform _carouselRoot;

        // 3Dキャラ選択用のCamera。
        [SerializeField] private Camera _selectCamera;

        // キャラ同士の横間隔。
        [SerializeField] private float _slotSpacing = 2.5f;

        // 選択中キャラが中央へ移動する速度。
        [SerializeField] private float _moveSpeed = 10.0f;

        // 3Dモデルクリック判定用Rayの距離。
        [SerializeField] private float _rayDistance = 1000.0f;

        [Header("Scale")]
        // 選択中キャラの左右に何体まで表示するか。
        [SerializeField] private int _visibleSideCount = 2;

        // 中央の選択中キャラの拡大率。
        [SerializeField] private float _selectedScale = 1.4f;

        // 選択中キャラの隣にいるキャラの拡大率。
        [SerializeField] private float _nearSideScale = 1.0f;

        // 選択中キャラから2つ離れたキャラの拡大率。
        [SerializeField] private float _farSideScale = 0.75f;

        [Header("UI")]
        // 選択中キャラ名を表示するText。
        [SerializeField] private TextMeshProUGUI _characterNameText;

        // 選択中キャラの説明文を表示するText。
        [SerializeField] private TextMeshProUGUI _characterDescriptionText;

        // 選択中キャラを確定するボタン。
        [SerializeField] private Button _decideButton;

        // キャラ選択画面を閉じるボタン。
        [SerializeField] private Button _closeButton;

        [Header("Drag")]
        // この値以上左右に動かしたらキャラを切り替える。
        [SerializeField] private float _dragThreshold = 80.0f;

        [Header("Gamepad")]
        // ゲームパッド左右入力を受け付けるしきい値。
        [SerializeField] private float _gamepadInputThreshold = 0.6f;

        // ゲームパッド長押し時の連続切り替え間隔。
        [SerializeField] private float _gamepadRepeatSeconds = 0.25f;

        [Header("Mouse")]
        [SerializeField] private GameObject _dragArea;

        [Header("Default Selection")]
        [SerializeField] private Selectable _firstSelectable;

        private enum CharacterSlideDirection
        {
            None,
            Next,
            Previous
        }

        // 生成したキャラ表示オブジェクトの状態一覧。
        private readonly List<CharacterPreviewState> _previewStates = new();

        // 現在選択中のIndex。
        private int _selectedIndex;

        private sealed class CharacterPreviewState
        {
            public CharacterPreviewState(
                GameObject sourcePrefab,
                CharacterPreviewSelectable selectable,
                CharacterStatus status,
                CharacterAttack attack,
                CharacterSuper characterSuper,
                Vector3 targetLocalPosition,
                float targetScale)
            {
                SourcePrefab = sourcePrefab;
                Selectable = selectable;
                Status = status;
                Attack = attack;
                CharacterSuper = characterSuper;
                TargetLocalPosition = targetLocalPosition;
                TargetScale = targetScale;
                IsActiveDuringMove = false;
                IsVisibleAfterMove = false;
            }

            public GameObject SourcePrefab
            {
                get;
            }

            public CharacterPreviewSelectable Selectable
            {
                get;
            }

            public CharacterStatus Status
            {
                get;
            }

            public CharacterAttack Attack
            {
                get;
            }

            public CharacterSuper CharacterSuper
            {
                get;
            }

            public string DisplayName =>
                SourcePrefab != null ? SourcePrefab.name : "未設定";

            public Vector3 TargetLocalPosition
            {
                get; set;
            }

            public float TargetScale
            {
                get; set;
            }

            public bool IsActiveDuringMove
            {
                get; set;
            }

            public bool IsVisibleAfterMove
            {
                get; set;
            }
        }

        // 次にゲームパッド入力を受け付ける時刻。
        private float _nextGamepadInputTime;

        // マウスドラッグ中かどうか。
        private bool _isMouseDragging;

        // 今回のマウス操作でドラッグによる切り替えが発生したか。
        private bool _hasMouseDragged;

        // マウスドラッグ開始位置。
        private Vector2 _mouseDragStartPosition;

        private void Awake()
        {
            // 自身の内部初期化として、キャラ表示を生成する。
            CreateCharacterPreviews();
        }

        private void OnEnable()
        {
            // ボタンの登録。
            if (_decideButton != null)
            {
                _decideButton.onClick.AddListener(HandleDecideButtonClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(HandleCloseButtonClicked);
            }
        }

        private void Start()
        {
            // 他オブジェクト依存の入力取得。
            if (_gameInput == null)
            {
                _gameInput = FindFirstObjectByType<GameInput>();
            }

            if (_gameInput != null)
            {
                _gameInput.EnableUIInput();
            }

            // Storeに保存されているキャラ、または先頭キャラを初期選択にする。
            ApplyInitialSelection();

            // 初期選択状態を見た目と説明文に反映する。
            RefreshSelection();
            ApplyPreviewStatesImmediately();

            // パッド操作用に最初のUIを選択する。
            SelectUi(_firstSelectable);
        }

        private void Update()
        {
            // 選択中キャラが中央に来るように、親を少しずつ移動する。
            MoveCharacterPreviews();

            // Cancelでキャラ選択画面を閉じる。
            HandleCancelInput();

            // ゲームパッドの左右入力。
            HandleGamepadInput();

            // マウスのドラッグ / クリック入力。
            HandleMouseInput();
        }

        private void OnDisable()
        {
            // ボタンの解除。
            if (_decideButton != null)
            {
                _decideButton.onClick.RemoveListener(HandleDecideButtonClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
            }
        }

        /// <summary>
        /// CharacterData配列から3Dキャラモデルを生成する。
        /// </summary>
        private void CreateCharacterPreviews()
        {
            if (_characterPrefabs == null || _characterPrefabs.Length == 0)
            {
                Debug.LogWarning("Character Prefabs が設定されていません。");
                return;
            }

            if (_carouselRoot == null)
            {
                Debug.LogWarning("CarouselRoot が設定されていません。");
                return;
            }

            for (int i = 0; i < _characterPrefabs.Length; i++)
            {
                GameObject characterPrefab = _characterPrefabs[i];

                if (characterPrefab == null)
                {
                    continue;
                }

                GameObject previewObject = Instantiate(
                    characterPrefab,
                    _carouselRoot
                );

                // プレビュー画面では、Prefab内のHPバーやReloadバー用Canvasを表示しない。
                HidePreviewCanvases(previewObject);

                previewObject.transform.localPosition = new Vector3(
                    i * _slotSpacing,
                    0.0f,
                    0.0f
                );

                previewObject.transform.localRotation = Quaternion.identity;

                CharacterPreviewSelectable selectable =
                    previewObject.GetComponent<CharacterPreviewSelectable>();

                if (selectable == null)
                {
                    selectable = previewObject.AddComponent<CharacterPreviewSelectable>();
                }

                selectable.Initialize(i);

                CharacterStatus status =
                    previewObject.GetComponentInChildren<CharacterStatus>(true);

                CharacterAttack attack =
                    previewObject.GetComponentInChildren<CharacterAttack>(true);

                CharacterSuper characterSuper =
                    previewObject.GetComponentInChildren<CharacterSuper>(true);

                _previewStates.Add(
                    new CharacterPreviewState(
                        characterPrefab,
                        selectable,
                        status,
                        attack,
                        characterSuper,
                        previewObject.transform.localPosition,
                        _farSideScale
                    )
                );
            }
        }

        /// <summary>
        /// プレビュー表示では、キャラPrefab内のCanvasを非表示にする。
        /// HPBarやReloadBarなど、ゲーム中用UIが見えるのを防ぐ。
        /// </summary>
        private void HidePreviewCanvases(GameObject previewObject)
        {
            Canvas[] canvases = previewObject.GetComponentsInChildren<Canvas>(true);

            foreach (Canvas canvas in canvases)
            {
                canvas.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Storeに保存されているキャラを初期選択にする。
        /// 未保存の場合は先頭キャラを選択する。
        /// </summary>
        private void ApplyInitialSelection()
        {
            if (_previewStates.Count == 0)
            {
                return;
            }

            if (_characterSelectionStore == null)
            {
                _selectedIndex = 0;
                return;
            }

            GameObject selectedCharacterPrefab =
                _characterSelectionStore.SelectedCharacterPrefab;

            for (int i = 0; i < _previewStates.Count; i++)
            {
                if (_previewStates[i].SourcePrefab == selectedCharacterPrefab)
                {
                    _selectedIndex = i;
                    return;
                }
            }

            _selectedIndex = 0;
        }

        /// <summary>
        /// 各キャラを目標位置・目標スケールへ少しずつ近づける。
        /// </summary>
        private void MoveCharacterPreviews()
        {
            for (int i = 0; i < _previewStates.Count; i++)
            {
                CharacterPreviewState previewState = _previewStates[i];

                if (previewState == null || previewState.Selectable == null)
                {
                    continue;
                }

                GameObject previewObject = previewState.Selectable.gameObject;

                if (!previewState.IsActiveDuringMove)
                {
                    if (previewObject.activeSelf)
                    {
                        previewObject.SetActive(false);
                    }

                    continue;
                }

                if (!previewObject.activeSelf)
                {
                    previewObject.SetActive(true);
                }

                Transform previewTransform = previewState.Selectable.transform;

                previewTransform.localPosition = Vector3.Lerp(
                    previewTransform.localPosition,
                    previewState.TargetLocalPosition,
                    Time.deltaTime * _moveSpeed
                );

                previewTransform.localScale = Vector3.Lerp(
                    previewTransform.localScale,
                    Vector3.one * previewState.TargetScale,
                    Time.deltaTime * _moveSpeed
                );
            }
        }

        /// <summary>
        /// マウスの左右ドラッグとクリック選択を処理する。
        /// UIイベントのOnDragに頼らず、Mouse.currentから直接読む。
        /// </summary>
        private void HandleMouseInput()
        {
            if (Mouse.current == null || EventSystem.current == null)
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Debug.Log($"Mouse pressed: {mousePosition}");

                if (!IsPointerOverDragArea(mousePosition))
                {
                    Debug.Log("DragArea上ではありません。");
                    return;
                }

                Debug.Log("Mouse drag start");

                _isMouseDragging = true;
                _hasMouseDragged = false;
                _mouseDragStartPosition = mousePosition;

                return;
            }

            if (!_isMouseDragging)
            {
                return;
            }

            if (Mouse.current.leftButton.isPressed)
            {
                float dragAmount = mousePosition.x - _mouseDragStartPosition.x;

                Debug.Log($"Mouse drag amount: {dragAmount}");

                if (Mathf.Abs(dragAmount) < _dragThreshold)
                {
                    return;
                }

                if (dragAmount < 0.0f)
                {
                    Debug.Log("Mouse SelectNext");
                    SelectNext();
                }
                else
                {
                    Debug.Log("Mouse SelectPrevious");
                    SelectPrevious();
                }

                _mouseDragStartPosition = mousePosition;
                _hasMouseDragged = true;

                return;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                Debug.Log("Mouse released");

                if (!_hasMouseDragged)
                {
                    TrySelectCharacterByScreenPosition(mousePosition);
                }

                _isMouseDragging = false;
            }
        }

        private bool IsPointerOverDragArea(Vector2 screenPosition)
        {
            if (_dragArea == null)
            {
                Debug.LogError("DragArea が設定されていません。");
                return false;
            }

            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> raycastResults = new();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            foreach (RaycastResult raycastResult in raycastResults)
            {
                Debug.Log($"UI Raycast Hit: {raycastResult.gameObject.name}");

                if (raycastResult.gameObject == _dragArea)
                {
                    return true;
                }

                if (raycastResult.gameObject.transform.IsChildOf(_dragArea.transform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 現在のマウス位置が、このControllerが付いている操作エリア上にあるか調べる。
        /// </summary>
        private bool IsPointerOverControlArea(Vector2 screenPosition)
        {
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> raycastResults = new();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            foreach (RaycastResult raycastResult in raycastResults)
            {
                // CharacterSelectSceneControllerがDragAreaに付いている前提。
                if (raycastResult.gameObject == gameObject)
                {
                    return true;
                }

                if (raycastResult.gameObject.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// ゲームパッドの左右入力でキャラを切り替える。
        /// </summary>
        private void HandleGamepadInput()
        {
            if (_gameInput == null)
            {
                return;
            }

            if (!IsCharacterFocusSelected())
            {
                return;
            }

            if (Time.unscaledTime < _nextGamepadInputTime)
            {
                return;
            }

            Vector2 navigate = _gameInput.UI.Navigate.ReadValue<Vector2>();

            if (navigate.x > _gamepadInputThreshold)
            {
                SelectNext();
                _nextGamepadInputTime = Time.unscaledTime + _gamepadRepeatSeconds;
                return;
            }

            if (navigate.x < -_gamepadInputThreshold)
            {
                SelectPrevious();
                _nextGamepadInputTime = Time.unscaledTime + _gamepadRepeatSeconds;
            }
        }

        private void HandleCancelInput()
        {
            if (_gameInput == null)
            {
                return;
            }

            if (!_gameInput.UI.Cancel.WasPressedThisFrame())
            {
                return;
            }

            HandleCloseButtonClicked();
        }

        /// <summary>
        /// 画面座標から3D空間へRayを飛ばし、クリックされたキャラを選択する。
        /// </summary>
        private void TrySelectCharacterByScreenPosition(Vector2 screenPosition)
        {
            Camera selectCamera = _selectCamera != null ? _selectCamera : Camera.main;

            if (selectCamera == null)
            {
                Debug.LogWarning("選択用Cameraが見つかりません。");
                return;
            }

            Ray ray = selectCamera.ScreenPointToRay(screenPosition);

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                _rayDistance,
                ~0,
                QueryTriggerInteraction.Collide
            );

            if (hits.Length == 0)
            {
                return;
            }

            System.Array.Sort(
                hits,
                (left, right) => left.distance.CompareTo(right.distance)
            );

            foreach (RaycastHit hit in hits)
            {
                CharacterPreviewSelectable selectable =
                    hit.collider.GetComponentInParent<CharacterPreviewSelectable>();

                if (selectable == null)
                {
                    continue;
                }

                SelectIndex(selectable.Index);
                return;
            }
        }

        /// <summary>
        /// 1つ右側のキャラを選択する。
        /// </summary>
        private void SelectNext()
        {
            if (_previewStates.Count == 0)
            {
                return;
            }

            SelectIndex(_selectedIndex + 1, CharacterSlideDirection.Next);
        }

        /// <summary>
        /// 1つ左側のキャラを選択する。
        /// </summary>
        private void SelectPrevious()
        {
            if (_previewStates.Count == 0)
            {
                return;
            }

            SelectIndex(_selectedIndex - 1, CharacterSlideDirection.Previous);
        }

        private void SelectIndex(int index)
        {
            SelectIndex(index, CharacterSlideDirection.None);
        }

        /// <summary>
        /// 指定Indexのキャラを選択する。
        /// 範囲外の場合はループさせる。
        /// </summary>
        private void SelectIndex(int index, CharacterSlideDirection slideDirection)
        {
            if (_previewStates.Count == 0)
            {
                return;
            }

            int previousIndex = _selectedIndex;
            int nextIndex = WrapIndex(index, _previewStates.Count);

            if (previousIndex == nextIndex)
            {
                return;
            }

            PrepareSlideStartPositions(previousIndex, nextIndex, slideDirection);

            _selectedIndex = nextIndex;

            RefreshSelectionTargets(previousIndex, slideDirection);
            RefreshCharacterInfo();
        }

        private int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (index % count + count) % count;
        }

        /// <summary>
        /// 選択状態に応じて、各キャラの目標位置・表示状態・説明文を更新する。
        /// </summary>
        private void RefreshSelection()
        {
            RefreshSelectionTargets(_selectedIndex, CharacterSlideDirection.None);
            RefreshCharacterInfo();
        }

        private void RefreshSelectionTargets(
            int previousIndex,
            CharacterSlideDirection slideDirection)
        {
            int visibleSideCount = GetVisibleSideCount();

            for (int i = 0; i < _previewStates.Count; i++)
            {
                CharacterPreviewState previewState = _previewStates[i];

                if (previewState == null || previewState.Selectable == null)
                {
                    continue;
                }

                int rightDistance = GetRightDistance(
                    _selectedIndex,
                    i,
                    _previewStates.Count
                );

                bool finalVisible = rightDistance <= visibleSideCount;

                int targetSlot = finalVisible
                    ? rightDistance
                    : visibleSideCount + 1;

                previewState.TargetLocalPosition = new Vector3(
                    targetSlot * _slotSpacing,
                    0.0f,
                    0.0f
                );

                previewState.TargetScale = GetScaleBySlot(targetSlot);
                previewState.IsActiveDuringMove = finalVisible;
                previewState.IsVisibleAfterMove = finalVisible;

                previewState.Selectable.gameObject.SetActive(finalVisible);
            }
        }

        private int GetRightDistance(int selectedIndex, int targetIndex, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (targetIndex - selectedIndex + count) % count;
        }

        private bool IsVisibleFromSelectedIndex(int selectedIndex, int targetIndex)
        {
            int rightDistance = GetRightDistance(
                selectedIndex,
                targetIndex,
                _previewStates.Count
            );

            return rightDistance <= GetVisibleSideCount();
        }

        private int GetVisibleSideCount()
        {
            return Mathf.Max(0, _visibleSideCount);
        }

        private float GetScaleBySlot(int slot)
        {
            int distance = Mathf.Abs(slot);

            return GetScaleByDistance(distance);
        }

        private void PrepareSlideStartPositions(
            int previousIndex,
            int nextIndex,
            CharacterSlideDirection slideDirection)
        {
            if (_previewStates.Count == 0)
            {
                return;
            }

            int visibleSideCount = GetVisibleSideCount();

            if (slideDirection == CharacterSlideDirection.Previous)
            {
                SetPreviewStartTransform(
                    nextIndex,
                    -_slotSpacing,
                    _farSideScale
                );

                return;
            }

            if (slideDirection == CharacterSlideDirection.Next)
            {
                int enteringRightIndex = WrapIndex(
                    nextIndex + visibleSideCount,
                    _previewStates.Count
                );

                if (enteringRightIndex == previousIndex)
                {
                    return;
                }

                SetPreviewStartTransform(
                    enteringRightIndex,
                    (visibleSideCount + 1) * _slotSpacing,
                    _farSideScale
                );
            }
        }

        private void SetPreviewStartTransform(int index, float localX, float scale)
        {
            if (index < 0 || index >= _previewStates.Count)
            {
                return;
            }

            CharacterPreviewState previewState = _previewStates[index];

            if (previewState == null || previewState.Selectable == null)
            {
                return;
            }

            GameObject previewObject = previewState.Selectable.gameObject;
            Transform previewTransform = previewState.Selectable.transform;

            previewObject.SetActive(true);

            previewTransform.localPosition = new Vector3(
                localX,
                0.0f,
                0.0f
            );

            previewTransform.localScale = Vector3.one * scale;
        }

        private void ApplyPreviewStatesImmediately()
        {
            for (int i = 0; i < _previewStates.Count; i++)
            {
                CharacterPreviewState previewState = _previewStates[i];

                if (previewState == null || previewState.Selectable == null)
                {
                    continue;
                }

                Transform previewTransform = previewState.Selectable.transform;

                previewTransform.localPosition = previewState.TargetLocalPosition;
                previewTransform.localScale = Vector3.one * previewState.TargetScale;

                previewState.Selectable.gameObject.SetActive(previewState.IsVisibleAfterMove);
            }
        }

        /// <summary>
        /// 選択中Indexからの距離に応じて拡大率を返す。
        /// </summary>
        private float GetScaleByDistance(int distance)
        {
            if (distance == 0)
            {
                return _selectedScale;
            }

            if (distance == 1)
            {
                return _nearSideScale;
            }

            if (distance == 2)
            {
                return _farSideScale;
            }

            return _farSideScale;
        }

        /// <summary>
        /// 左側の説明欄に、選択中キャラの名前と説明を表示する。
        /// </summary>
        private void RefreshCharacterInfo()
        {
            CharacterPreviewState previewState = GetSelectedPreviewState();

            if (previewState == null)
            {
                if (_characterNameText != null)
                {
                    _characterNameText.text = "未選択";
                }

                if (_characterDescriptionText != null)
                {
                    _characterDescriptionText.text = string.Empty;
                }

                return;
            }

            if (_characterNameText != null)
            {
                _characterNameText.text = previewState.DisplayName;
            }

            if (_characterDescriptionText != null)
            {
                _characterDescriptionText.text =
                    CreateCharacterDescription(previewState);
            }
        }

        private CharacterPreviewState GetSelectedPreviewState()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _previewStates.Count)
            {
                return null;
            }

            return _previewStates[_selectedIndex];
        }

        private string CreateCharacterDescription(CharacterPreviewState previewState)
        {
            CharacterStatus status = previewState.Status;

            float maxHp = status != null ? status.MaxHp : 0f;
            float maxReloadCount = status != null ? status.MaxReloadCount : 0f;
            float reloadRecoveryPerSecond =
                status != null ? status.ReloadRecoveryPerSecond : 0f;

            CharacterSkillPreviewInfo attackInfo =
                previewState.Attack != null
                    ? previewState.Attack.PreviewInfo
                    : default;

            CharacterSkillPreviewInfo superInfo =
                previewState.CharacterSuper != null
                    ? previewState.CharacterSuper.PreviewInfo
                    : default;

            return
                $"HP: {maxHp:0}\n" +
                $"リロード数: {maxReloadCount:0}\n" +
                $"リロード回復: {reloadRecoveryPerSecond:0.##}/秒\n\n" +
                $"通常攻撃\n{FormatSkillInfo(attackInfo)}\n\n" +
                $"必殺技\n{FormatSkillInfo(superInfo)}";
        }

        private string FormatSkillInfo(CharacterSkillPreviewInfo info)
        {
            if (!info.IsValid)
            {
                return "未設定";
            }

            return
                $"弾数: {info.BulletCount}\n" +
                $"合計ダメージ: {info.TotalDamage:0}\n" +
                $"射程: {info.MaxRange:0.##}\n" +
                $"弾速: {info.MaxSpeed:0.##}";
        }

        /// <summary>
        /// 現在選択中のCharacterDataを取得する。
        /// </summary>
        private GameObject GetSelectedCharacterPrefab()
        {
            CharacterPreviewState previewState = GetSelectedPreviewState();

            if (previewState == null)
            {
                return null;
            }

            return previewState.SourcePrefab;
        }

        /// <summary>
        /// 選択中キャラを確定してロビー側へ反映する。
        /// </summary>
        private void HandleDecideButtonClicked()
        {
            GameObject selectedCharacterPrefab = GetSelectedCharacterPrefab();

            if (selectedCharacterPrefab == null)
            {
                Debug.LogWarning("選択中のキャラPrefabがありません。");
                return;
            }

            if (_characterSelectionStore == null)
            {
                Debug.LogError("CharacterSelectionStore が設定されていません。");
                return;
            }

            _characterSelectionStore.SetSelectedCharacterPrefab(selectedCharacterPrefab);

            SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        /// <summary>
        /// キャラ選択画面を閉じる。
        /// </summary>
        private void HandleCloseButtonClicked()
        {
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }

        private bool IsCharacterFocusSelected()
        {
            if (_firstSelectable == null)
            {
                return false;
            }

            if (EventSystem.current == null)
            {
                return false;
            }

            GameObject selectedGameObject = EventSystem.current.currentSelectedGameObject;

            if (selectedGameObject == null)
            {
                return false;
            }

            return selectedGameObject == _firstSelectable.gameObject;
        }

        private void SelectUi(Selectable selectable)
        {
            if (selectable == null)
            {
                return;
            }

            if (EventSystem.current == null)
            {
                return;
            }

            if (!selectable.gameObject.activeInHierarchy)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
