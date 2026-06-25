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
        // キャラ選択画面に表示するキャラPrefab一覧。
        [SerializeField] private GameObject[] _characterPrefabs;

        [Header("Preview")]
        // 3Dキャラモデルを横並びで生成する親。
        [SerializeField] private Transform _carouselRoot;

        // 3Dキャラクリック判定用のCamera。
        // 未設定の場合はCamera.mainを使う。
        [SerializeField] private Camera _selectCamera;

        // キャラ同士の横間隔。
        [SerializeField] private float _slotSpacing = 2.5f;

        // キャラが目標位置へ移動する速度。
        [SerializeField] private float _moveSpeed = 10.0f;

        // 3Dモデルクリック判定用Rayの距離。
        [SerializeField] private float _rayDistance = 1000.0f;

        [Header("Scale")]
        // 選択中キャラの右側に何体まで表示するか。
        [SerializeField] private int _visibleSideCount = 2;

        // 中央の選択中キャラの拡大率。
        [SerializeField] private float _selectedScale = 1.4f;

        // 選択中キャラの隣にいるキャラの拡大率。
        [SerializeField] private float _nearSideScale = 1.0f;

        // 選択中キャラから2つ以上離れたキャラの拡大率。
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
        // マウスドラッグ / クリックを受け付けるUI領域。
        [SerializeField] private GameObject _dragArea;

        [Header("Default Selection")]
        // キャラ選択Sceneを開いた時に最初に選択するUI。
        // Pad操作用の透明なCharacterFocusButtonなどを入れる想定。
        [SerializeField] private Selectable _firstSelectable;

        // 通常移動が完了したとみなす距離。
        private const float MoveCompleteDistance = 0.05f;

        // 退場するキャラを非表示にしてよい距離。
        // スケールが完全に縮み切るまで待つと遅いので、位置だけで判定する。
        private const float ExitCompleteDistance = 0.2f;

        // 生成したキャラ表示オブジェクトの状態一覧。
        private readonly List<CharacterPreviewState> _previewStates = new();

        // 現在選択中のIndex。
        private int _selectedIndex;

        // 次にゲームパッド入力を受け付ける時刻。
        private float _nextGamepadInputTime;

        // マウスドラッグ中かどうか。
        private bool _isMouseDragging;

        // 今回のマウス操作でドラッグによる切り替えが発生したか。
        private bool _hasMouseDragged;

        // キャラのスクロール移動中かどうか。
        // 移動中は連続入力で目標位置が壊れないように入力を止める。
        private bool _isScrolling;

        // マウスドラッグ開始位置。
        private Vector2 _mouseDragStartPosition;

        /// <summary>
        /// 生成したプレビューキャラ1体分の状態。
        /// </summary>
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

            // 元になったキャラPrefab。
            public GameObject SourcePrefab
            {
                get;
            }

            // 3Dモデルクリック時にIndexを取得するための選択用コンポーネント。
            public CharacterPreviewSelectable Selectable
            {
                get;
            }

            // 表示用ステータス取得元。
            public CharacterStatus Status
            {
                get;
            }

            // 通常攻撃情報取得元。
            public CharacterAttack Attack
            {
                get;
            }

            // 必殺技情報取得元。
            public CharacterSuper CharacterSuper
            {
                get;
            }

            // 表示名。
            // 現状はPrefab名をそのまま表示する。
            public string DisplayName =>
                SourcePrefab != null ? SourcePrefab.name : "未設定";

            // 移動先のローカル座標。
            public Vector3 TargetLocalPosition
            {
                get; set;
            }

            // 移動先のスケール倍率。
            public float TargetScale
            {
                get; set;
            }

            // 移動中に表示対象として扱うか。
            public bool IsActiveDuringMove
            {
                get; set;
            }

            // 移動完了後も表示するか。
            public bool IsVisibleAfterMove
            {
                get; set;
            }
        }

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
            // 各キャラを目標位置へ移動させる。
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
        /// キャラPrefab配列から3Dキャラモデルを生成する。
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

                // 退場予定のキャラは、外側へある程度流れたら非表示にする。
                // スケールが完全一致するまで待つと、消えるまでが長く見えるため位置だけで判定する。
                if (!previewState.IsVisibleAfterMove &&
                    IsPreviewReachedTargetPosition(
                        previewState,
                        ExitCompleteDistance
                    ))
                {
                    previewObject.SetActive(false);
                    previewState.IsActiveDuringMove = false;
                }
            }

            // スクロール中の全キャラが目標位置に近づいたら、最終表示状態に整える。
            if (_isScrolling && IsPreviewMovementComplete())
            {
                CompleteScroll();
            }
        }

        /// <summary>
        /// スクロール完了時に、選択状態を最終配置へ整える。
        /// </summary>
        private void CompleteScroll()
        {
            _isScrolling = false;

            RefreshSelection();
            ApplyPreviewStatesImmediately();
        }

        /// <summary>
        /// 指定キャラが目標位置に十分近づいたか調べる。
        /// </summary>
        private bool IsPreviewReachedTargetPosition(
            CharacterPreviewState previewState,
            float completeDistance)
        {
            if (previewState == null || previewState.Selectable == null)
            {
                return true;
            }

            Transform previewTransform = previewState.Selectable.transform;

            float positionDistance = Vector3.Distance(
                previewTransform.localPosition,
                previewState.TargetLocalPosition
            );

            return positionDistance <= completeDistance;
        }

        /// <summary>
        /// スクロール移動が完了したか調べる。
        /// 入力ロック解除の判定に使う。
        /// </summary>
        private bool IsPreviewMovementComplete()
        {
            for (int i = 0; i < _previewStates.Count; i++)
            {
                CharacterPreviewState previewState = _previewStates[i];

                if (previewState == null || previewState.Selectable == null)
                {
                    continue;
                }

                if (!previewState.IsActiveDuringMove)
                {
                    continue;
                }

                // 退場中のキャラがまだ残っている場合は、完了扱いにしない。
                if (!previewState.IsVisibleAfterMove)
                {
                    return false;
                }

                if (!IsPreviewReachedTargetPosition(
                        previewState,
                        MoveCompleteDistance
                    ))
                {
                    return false;
                }
            }

            return true;
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

            if (_isScrolling)
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverDragArea(mousePosition))
                {
                    _isMouseDragging = false;
                    return;
                }

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

                if (Mathf.Abs(dragAmount) < _dragThreshold)
                {
                    return;
                }

                // 左へドラッグしたら右側のキャラを選択する。
                if (dragAmount < 0.0f)
                {
                    SelectNext();
                }
                else
                {
                    SelectPrevious();
                }

                _mouseDragStartPosition = mousePosition;
                _hasMouseDragged = true;

                return;
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                // ドラッグが発生していない場合だけ、クリック選択として扱う。
                if (!_hasMouseDragged)
                {
                    TrySelectCharacterByScreenPosition(mousePosition);
                }

                _isMouseDragging = false;
            }
        }

        /// <summary>
        /// 現在のマウス位置が、ドラッグ操作エリア上にあるか調べる。
        /// </summary>
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
        /// ゲームパッドの左右入力でキャラを切り替える。
        /// </summary>
        private void HandleGamepadInput()
        {
            if (_gameInput == null)
            {
                return;
            }

            if (_isScrolling)
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

        /// <summary>
        /// Cancel入力でキャラ選択画面を閉じる。
        /// </summary>
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

                SelectClickedCharacter(selectable.Index);
                return;
            }
        }

        /// <summary>
        /// クリックされたキャラを選択する。
        /// 現在位置から見て近い方向へ、必要スロット数分まとめてスクロールする。
        /// </summary>
        private void SelectClickedCharacter(int clickedIndex)
        {
            if (_previewStates.Count == 0)
            {
                return;
            }

            int targetIndex = WrapIndex(clickedIndex, _previewStates.Count);

            if (targetIndex == _selectedIndex)
            {
                return;
            }

            int rightDistance = GetRightDistance(
                _selectedIndex,
                targetIndex,
                _previewStates.Count
            );

            int leftDistance = GetRightDistance(
                targetIndex,
                _selectedIndex,
                _previewStates.Count
            );

            if (rightDistance <= leftDistance)
            {
                SelectIndexByScroll(targetIndex, rightDistance);
                return;
            }

            SelectIndexByScroll(targetIndex, -leftDistance);
        }

        /// <summary>
        /// 1つ右側のキャラを選択する。
        /// </summary>
        private void SelectNext()
        {
            SelectIndexByScroll(_selectedIndex + 1, 1);
        }

        /// <summary>
        /// 1つ左側のキャラを選択する。
        /// </summary>
        private void SelectPrevious()
        {
            SelectIndexByScroll(_selectedIndex - 1, -1);
        }

        /// <summary>
        /// 指定Indexのキャラを選択し、指定スロット数分スクロールさせる。
        /// scrollSlotCountが正なら右側のキャラを中央へ流す。
        /// scrollSlotCountが負なら左側のキャラを中央へ流す。
        /// </summary>
        private void SelectIndexByScroll(int index, int scrollSlotCount)
        {
            if (_previewStates.Count == 0)
            {
                return;
            }

            int previousIndex = _selectedIndex;
            int nextIndex = WrapIndex(index, _previewStates.Count);

            if (previousIndex == nextIndex || scrollSlotCount == 0)
            {
                return;
            }

            _selectedIndex = nextIndex;
            _isScrolling = true;

            RefreshSelectionTargetsByScroll(previousIndex, scrollSlotCount);
            RefreshCharacterInfo();
        }

        /// <summary>
        /// スクロール方向に応じて、各キャラの目標位置を更新する。
        /// </summary>
        private void RefreshSelectionTargetsByScroll(
            int previousIndex,
            int scrollSlotCount)
        {
            if (scrollSlotCount > 0)
            {
                RefreshNextScrollTargets(previousIndex, scrollSlotCount);
                return;
            }

            RefreshPreviousScrollTargets(previousIndex, -scrollSlotCount);
        }

        /// <summary>
        /// 右側のキャラを中央へ持ってくるスクロール。
        /// 左ドラッグ / 十字キー右 / 右側キャラクリックで使う。
        /// </summary>
        private void RefreshNextScrollTargets(
            int previousIndex,
            int scrollAmount)
        {
            int count = _previewStates.Count;
            int visibleSideCount = GetVisibleSideCount();

            for (int i = 0; i < _previewStates.Count; i++)
            {
                CharacterPreviewState previewState = _previewStates[i];

                if (previewState == null || previewState.Selectable == null)
                {
                    continue;
                }

                int previousSlot = GetRightDistance(
                    previousIndex,
                    i,
                    count
                );

                int targetSlot = GetRightDistance(
                    _selectedIndex,
                    i,
                    count
                );

                bool wasVisible = previousSlot <= visibleSideCount;
                bool willVisible = targetSlot <= visibleSideCount;

                // 左側へ押し出されるキャラ。
                // 右側から出現する動きの逆として、左へ流しながら遠距離サイズへ縮める。
                bool exitsLeft =
                    wasVisible && previousSlot < scrollAmount;

                if (exitsLeft)
                {
                    int exitSlot = previousSlot - scrollAmount;

                    SetPreviewMoveTarget(
                        previewState,
                        exitSlot,
                        _farSideScale,
                        true,
                        false
                    );

                    continue;
                }

                if (willVisible)
                {
                    // 新しく右側から入ってくるキャラは、右奥から開始させる。
                    if (!wasVisible)
                    {
                        int startSlot = targetSlot + scrollAmount;

                        SetPreviewStartTransform(
                            previewState,
                            startSlot,
                            _farSideScale
                        );
                    }

                    SetPreviewMoveTarget(
                        previewState,
                        targetSlot,
                        GetScaleBySlot(targetSlot),
                        true,
                        true
                    );

                    continue;
                }

                HidePreviewImmediately(previewState);
            }
        }

        /// <summary>
        /// 左側のキャラを中央へ持ってくるスクロール。
        /// 右ドラッグ / 十字キー左で使う。
        /// </summary>
        private void RefreshPreviousScrollTargets(
            int previousIndex,
            int scrollAmount)
        {
            int count = _previewStates.Count;
            int visibleSideCount = GetVisibleSideCount();

            for (int i = 0; i < _previewStates.Count; i++)
            {
                CharacterPreviewState previewState = _previewStates[i];

                if (previewState == null || previewState.Selectable == null)
                {
                    continue;
                }

                int previousSlot = GetRightDistance(
                    previousIndex,
                    i,
                    count
                );

                int targetSlot = GetRightDistance(
                    _selectedIndex,
                    i,
                    count
                );

                bool wasVisible = previousSlot <= visibleSideCount;
                bool willVisible = targetSlot <= visibleSideCount;

                // 右側へ押し出されるキャラ。
                // 左側から出現する動きの逆として、右へ流しながら遠距離サイズへ縮める。
                bool exitsRight =
                    wasVisible && previousSlot > visibleSideCount - scrollAmount;

                if (exitsRight)
                {
                    int exitSlot = previousSlot + scrollAmount;

                    SetPreviewMoveTarget(
                        previewState,
                        exitSlot,
                        _farSideScale,
                        true,
                        false
                    );

                    continue;
                }

                if (willVisible)
                {
                    // 新しく左側から入ってくるキャラは、左奥から開始させる。
                    if (!wasVisible)
                    {
                        int startSlot = targetSlot - scrollAmount;

                        SetPreviewStartTransform(
                            previewState,
                            startSlot,
                            _farSideScale
                        );
                    }

                    SetPreviewMoveTarget(
                        previewState,
                        targetSlot,
                        GetScaleBySlot(targetSlot),
                        true,
                        true
                    );

                    continue;
                }

                HidePreviewImmediately(previewState);
            }
        }

        /// <summary>
        /// 現在の選択Indexに基づいて、最終的な表示状態を作る。
        /// 初期表示やスクロール完了後の整列で使う。
        /// </summary>
        private void RefreshSelection()
        {
            RefreshSelectionTargets();
            RefreshCharacterInfo();
        }

        /// <summary>
        /// 各キャラを、現在の選択Indexから見た最終位置へ設定する。
        /// </summary>
        private void RefreshSelectionTargets()
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

                bool visible = rightDistance <= visibleSideCount;

                int targetSlot = visible
                    ? rightDistance
                    : visibleSideCount + 1;

                SetPreviewMoveTarget(
                    previewState,
                    targetSlot,
                    GetScaleBySlot(targetSlot),
                    visible,
                    visible
                );
            }
        }

        /// <summary>
        /// indexを範囲内にループさせる。
        /// </summary>
        private int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (index % count + count) % count;
        }

        /// <summary>
        /// selectedIndexからtargetIndexまで、右方向に何スロット離れているかを返す。
        /// </summary>
        private int GetRightDistance(int selectedIndex, int targetIndex, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (targetIndex - selectedIndex + count) % count;
        }

        /// <summary>
        /// 表示する右側キャラ数を返す。
        /// </summary>
        private int GetVisibleSideCount()
        {
            return Mathf.Max(0, _visibleSideCount);
        }

        /// <summary>
        /// スロット位置に応じたスケールを返す。
        /// </summary>
        private float GetScaleBySlot(int slot)
        {
            int distance = Mathf.Abs(slot);

            return GetScaleByDistance(distance);
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

            return _farSideScale;
        }

        /// <summary>
        /// キャラの開始位置とスケールを即時設定する。
        /// 新しく画面内に入ってくるキャラの初期位置に使う。
        /// </summary>
        private void SetPreviewStartTransform(
            CharacterPreviewState previewState,
            int slot,
            float scale)
        {
            if (previewState == null || previewState.Selectable == null)
            {
                return;
            }

            Transform previewTransform = previewState.Selectable.transform;

            previewState.Selectable.gameObject.SetActive(true);

            previewTransform.localPosition = new Vector3(
                slot * _slotSpacing,
                0.0f,
                0.0f
            );

            previewTransform.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// キャラの移動目標を設定する。
        /// activeDuringMoveがfalseなら移動対象外として扱う。
        /// visibleAfterMoveがfalseなら到達後に非表示にする。
        /// </summary>
        private void SetPreviewMoveTarget(
            CharacterPreviewState previewState,
            int slot,
            float targetScale,
            bool activeDuringMove,
            bool visibleAfterMove)
        {
            if (previewState == null || previewState.Selectable == null)
            {
                return;
            }

            previewState.TargetLocalPosition = new Vector3(
                slot * _slotSpacing,
                0.0f,
                0.0f
            );

            previewState.TargetScale = targetScale;
            previewState.IsActiveDuringMove = activeDuringMove;
            previewState.IsVisibleAfterMove = visibleAfterMove;

            previewState.Selectable.gameObject.SetActive(activeDuringMove);
        }

        /// <summary>
        /// 指定キャラを即時非表示にする。
        /// 画面外で今回の移動にも関係ないキャラに使う。
        /// </summary>
        private void HidePreviewImmediately(CharacterPreviewState previewState)
        {
            if (previewState == null || previewState.Selectable == null)
            {
                return;
            }

            previewState.IsActiveDuringMove = false;
            previewState.IsVisibleAfterMove = false;

            previewState.Selectable.gameObject.SetActive(false);
        }

        /// <summary>
        /// 現在の目標位置・スケールを即座に反映する。
        /// 初期表示やスクロール完了後の整列に使う。
        /// </summary>
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

                previewState.Selectable.gameObject.SetActive(
                    previewState.IsVisibleAfterMove
                );
            }
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

        /// <summary>
        /// 現在選択中のプレビュー状態を取得する。
        /// </summary>
        private CharacterPreviewState GetSelectedPreviewState()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _previewStates.Count)
            {
                return null;
            }

            return _previewStates[_selectedIndex];
        }

        /// <summary>
        /// キャラ説明文を作成する。
        /// CharacterStatus / CharacterAttack / CharacterSuper から表示用情報を取得する。
        /// </summary>
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

        /// <summary>
        /// 攻撃 / 必殺技の表示用情報を文字列にする。
        /// </summary>
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
        /// 現在選択中のキャラPrefabを取得する。
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

        /// <summary>
        /// キャラ操作用のUIが選択されているか調べる。
        /// Padの左右入力でキャラだけを切り替えるために使う。
        /// </summary>
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

        /// <summary>
        /// 指定したUIを選択状態にする。
        /// </summary>
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
