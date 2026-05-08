using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Uraty.Feature.Akane_TestCharacter;
using Uraty.Systems.Input;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// Additiveで読み込まれるキャラ選択シーンを管理するクラス。
    /// キャラPrefabの生成、クリック選択、決定、シーンを閉じる処理を担当する。
    /// </summary>
    public sealed class CharacterSelectSceneController : MonoBehaviour
    {
        [Header("Input")]
        // 入力管理クラス。
        // Inspectorで未設定の場合は、StartでHierarchy上から探す。
        [SerializeField] private GameInput _gameInput;

        [Header("Characters")]
        // キャラ選択画面に表示するキャラデータ一覧。
        [SerializeField] private CharacterData[] _characters;

        [Header("Preview Slots")]
        // 各キャラPrefabを生成する位置。
        // Characters配列と順番を合わせる。
        [SerializeField] private Transform[] _previewSlots;

        [Header("View")]
        // 現在選択中のキャラ名を表示するText。
        [SerializeField] private TextMeshProUGUI _selectedCharacterNameText;

        [Header("Buttons")]
        // 選択中キャラを確定するボタン。
        [SerializeField] private Button _decideButton;

        // キャラ選択画面を閉じるボタン。
        [SerializeField] private Button _closeButton;

        [Header("Selection")]
        // 選択中キャラをどれくらい大きく見せるか。
        [SerializeField] private float _selectedScale = 1.15f;

        // Raycastの最大距離。
        [SerializeField] private float _rayDistance = 1000f;

        [Header("Camera")]
        // キャラ選択用のCamera。
        // 未設定の場合はCamera.mainを使う。
        [SerializeField] private Camera _selectCamera;

        // 生成したキャラ選択用オブジェクトを保持するリスト。
        // 選択状態の更新に使う。
        private readonly List<CharacterPreviewSelectable> _selectables = new();

        // 現在選択中のキャラデータ。
        private CharacterData _selectedCharacter;

        private void Awake()
        {
            // 決定ボタンと閉じるボタンに処理を登録する。
            _decideButton.onClick.AddListener(Decide);
            _closeButton.onClick.AddListener(Close);

            // CharacterData配列をもとに、キャラPrefabをSlotへ生成する。
            CreateCharacterPreviews();

            // すでにロビー側で選択されているキャラがあれば、それを初期選択にする。
            CharacterData currentCharacter = CharacterSelectionStore.SelectedCharacter;

            if (currentCharacter != null)
            {
                SelectCharacter(currentCharacter);
            }
            else if (_characters != null && _characters.Length > 0)
            {
                // まだ選択キャラがない場合は、先頭のキャラを初期選択にする。
                SelectCharacter(_characters[0]);
            }
        }

        private void Start()
        {
            // InspectorでGameInputが設定されていない場合、Hierarchy上から探す。
            if (_gameInput == null)
            {
                _gameInput = FindFirstObjectByType<GameInput>();
            }

            if (_gameInput == null)
            {
                Debug.LogError("GameInput が見つかりません。Systemsシーンなどに GameInput を配置してください。");
                return;
            }

            // キャラ選択画面ではUI入力を使う。
            _gameInput.EnableUIInput();
        }

        private void Update()
        {
            if (_gameInput == null)
            {
                return;
            }

            // Cancel入力でキャラ選択画面を閉じる。
            if (_gameInput.UI.Cancel.WasPressedThisFrame())
            {
                Close();
                return;
            }

            // Submit入力で、ポインタ位置から3Dキャラを選択する。
            if (_gameInput.UI.Submit.WasPressedThisFrame())
            {
                TrySelectCharacterByPoint();
            }
        }

        private void OnDestroy()
        {
            // 登録したボタン処理を解除する。
            if (_decideButton != null)
            {
                _decideButton.onClick.RemoveListener(Decide);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
            }
        }

        /// <summary>
        /// CharacterData配列をもとに、各SlotへキャラPrefabを生成する。
        /// </summary>
        private void CreateCharacterPreviews()
        {
            if (_characters == null || _characters.Length == 0)
            {
                Debug.LogWarning("Characters が設定されていません。");
                return;
            }

            if (_previewSlots == null || _previewSlots.Length == 0)
            {
                Debug.LogWarning("Preview Slots が設定されていません。");
                return;
            }

            // キャラ数とSlot数の少ない方に合わせる。
            int count = Mathf.Min(_characters.Length, _previewSlots.Length);

            for (int i = 0; i < count; i++)
            {
                CharacterData character = _characters[i];
                Transform slot = _previewSlots[i];

                if (character == null || slot == null)
                {
                    continue;
                }

                if (character.PreviewPrefab == null)
                {
                    Debug.LogWarning($"{character.DisplayName} に PreviewPrefab が設定されていません。");
                    continue;
                }

                // Slotの位置にキャラPrefabを生成する。
                GameObject previewObject = Instantiate(
                    character.PreviewPrefab,
                    slot.position,
                    slot.rotation,
                    slot
                );

                // クリック選択用のComponentを取得する。
                CharacterPreviewSelectable selectable =
                    previewObject.GetComponent<CharacterPreviewSelectable>();

                // Prefab側に付いていない場合は、自動で追加する。
                if (selectable == null)
                {
                    selectable = previewObject.AddComponent<CharacterPreviewSelectable>();
                }

                // この表示キャラがどのCharacterDataに対応するかを登録する。
                selectable.Initialize(character);

                // 選択状態の更新用に保持する。
                _selectables.Add(selectable);
            }
        }

        /// <summary>
        /// 現在のポインタ位置からRayを飛ばし、当たったキャラを選択する。
        /// </summary>
        private void TrySelectCharacterByPoint()
        {
            // 専用Cameraが設定されていればそれを使う。
            // 未設定ならCamera.mainを使う。
            Camera mainCamera = _selectCamera != null ? _selectCamera : Camera.main;

            if (mainCamera == null)
            {
                Debug.LogWarning("選択用Cameraが見つかりません。");
                return;
            }

            // GameInputのUI/Pointから、画面上のポインタ座標を取得する。
            Vector2 pointerPosition = _gameInput.UI.Point.ReadValue<Vector2>();

            Debug.Log($"Pointer Position: {pointerPosition}");
            Debug.Log($"Screen Size: {Screen.width}, {Screen.height}");

            // 画面座標から3D空間へRayを飛ばす。
            Ray ray = mainCamera.ScreenPointToRay(pointerPosition);

            // Sceneビュー上でRayの方向を確認するためのデバッグ表示。
            Debug.DrawRay(ray.origin, ray.direction * _rayDistance, Color.red, 2f);

            // Rayに当たったすべてのColliderを取得する。
            // Triggerも対象にするため、QueryTriggerInteraction.Collideを指定している。
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                _rayDistance,
                ~0,
                QueryTriggerInteraction.Collide
            );

            if (hits.Length == 0)
            {
                Debug.Log("Raycastが何にも当たっていません。");
                return;
            }

            // どのColliderに当たったか確認するためのログ。
            foreach (RaycastHit hit in hits)
            {
                Debug.Log($"Raycast hit: {hit.collider.name}");
            }

            // 当たったColliderの親からCharacterPreviewSelectableを探す。
            foreach (RaycastHit hit in hits)
            {
                CharacterPreviewSelectable selectable =
                    hit.collider.GetComponentInParent<CharacterPreviewSelectable>();

                if (selectable == null)
                {
                    continue;
                }

                // 対応するキャラを選択状態にする。
                SelectCharacter(selectable.Character);
                return;
            }

            Debug.Log("Rayは何かに当たったが、CharacterPreviewSelectable は見つかりませんでした。");
        }

        /// <summary>
        /// 指定されたキャラを選択中キャラとして扱う。
        /// 表示名と選択中の見た目も更新する。
        /// </summary>
        private void SelectCharacter(CharacterData character)
        {
            if (character == null)
            {
                return;
            }

            _selectedCharacter = character;

            // 選択中キャラ名をUIに表示する。
            if (_selectedCharacterNameText != null)
            {
                _selectedCharacterNameText.text = character.DisplayName;
            }

            // 全キャラの選択状態を更新する。
            foreach (CharacterPreviewSelectable selectable in _selectables)
            {
                if (selectable == null)
                {
                    continue;
                }

                bool isSelected = selectable.Character == character;
                selectable.SetSelected(isSelected, _selectedScale);
            }

            Debug.Log($"選択中キャラ: {character.DisplayName}");
        }

        /// <summary>
        /// 現在選択中のキャラを確定し、ロビー側へ反映する。
        /// </summary>
        private void Decide()
        {
            if (_selectedCharacter == null)
            {
                Debug.LogWarning("選択中のキャラがありません。");
                return;
            }

            // Storeへ保存することで、ロビー側の表示更新を発生させる。
            CharacterSelectionStore.SetSelectedCharacter(_selectedCharacter);

            Debug.Log($"決定したキャラ: {_selectedCharacter.DisplayName}");

            Close();
        }

        /// <summary>
        /// このAdditive Sceneを閉じる。
        /// </summary>
        private void Close()
        {
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }
    }
}
