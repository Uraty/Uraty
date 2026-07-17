using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using Uraty.Feature.Akane_GameMode;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// ロビーのモード選択画面を管理するクラス。
    /// モード一覧ボタンを生成し、選択されたモードを保持する。
    /// </summary>
    public sealed class LobbyModeSelectController : MonoBehaviour
    {
        [Header("Panels")]
        // ロビー通常画面。
        [SerializeField] private GameObject _mainPanel;

        // ロビー中央の3Dキャラ表示などを含むPanel。
        [SerializeField] private GameObject _modelPanel;

        // モード選択画面。
        [SerializeField] private GameObject _modePanel;

        [Header("Main")]
        // MainPanel側にある、モード選択画面を開くボタン。
        [SerializeField] private Button _openModeButton;

        // MainPanel側に表示する、現在選択中のモード名。
        [SerializeField] private TextMeshProUGUI _selectedModeText;

        [Header("Mode Panel")]
        // モード選択画面を閉じるボタン。
        [SerializeField] private Button _closeButton;

        // 生成したモードボタンを並べる親。
        // ScrollView / Viewport / Content を入れる想定。
        [SerializeField] private Transform _modeButtonParent;

        // モード1つ分の表示ボタンPrefab。
        [SerializeField] private ModeButtonView _modeButtonPrefab;

        [Header("Mode Data")]
        // 表示するモードデータ一覧。
        [SerializeField] private GameModeData[] _modes;

        [Header("Default Selection")]
        [SerializeField] private Selectable _firstMainSelectable;

        [Header("Input")]
        [SerializeField] private InputActionReference _cancelActionReference;

        [Header("Audio")]
        [SerializeField] private LobbyAudioController _audioController;

        private readonly List<ModeButtonView> _modeButtonViews = new();

        /// <summary>
        /// 現在選択中のモード。
        /// PlayButtonを押したときに、この値を使ってシーン遷移する。
        /// </summary>
        public GameModeData SelectedMode
        {
            get; private set;
        }

        private void Awake()
        {
            // MainPanelのモードボタンを押したらモード選択画面を開く。
            _openModeButton.onClick.AddListener(OpenModePanel);

            // モード選択画面の閉じるボタン。
            _closeButton.onClick.AddListener(CloseModePanel);

            // ScriptableObjectのモードデータから、ボタンを自動生成する。
            CreateModeButtons();

            // 初期モードを設定する。
            if (_modes != null && _modes.Length > 0)
            {
                SelectMode(_modes[0], false);
            }

            // 起動時はモード選択画面を閉じた状態にする。
            CloseModePanel();
        }

        private void OnEnable()
        {
            if (_cancelActionReference != null)
            {
                _cancelActionReference.action.performed += HandleCancelPerformed;

                if (!_cancelActionReference.action.enabled)
                {
                    _cancelActionReference.action.Enable();
                }
            }
        }

        private void OnDisable()
        {
            if (_cancelActionReference != null)
            {
                _cancelActionReference.action.performed -= HandleCancelPerformed;
            }
        }

        /// <summary>
        /// _modes配列の内容に応じて、モード選択ボタンを生成する。
        /// </summary>
        private void CreateModeButtons()
        {
            if (_modes == null)
            {
                return;
            }

            foreach (GameModeData mode in _modes)
            {
                if (mode == null)
                {
                    continue;
                }

                ModeButtonView buttonView = Instantiate(
                    _modeButtonPrefab,
                    _modeButtonParent
                );

                // ボタンにモード情報とクリック時処理を渡す。
                buttonView.Initialize(mode, OnModeButtonClicked);
                _modeButtonViews.Add(buttonView);
            }
        }

        /// <summary>
        /// モードボタンが押されたときに呼ばれる。
        /// </summary>
        private void OnModeButtonClicked(GameModeData mode)
        {
            _audioController?.PlayButtonPressedSe();

            SelectMode(mode, true);
        }

        /// <summary>
        /// 選択中モードを更新する。
        /// closeAfterSelectがtrueなら、選択後にロビーへ戻る。
        /// </summary>
        private void SelectMode(GameModeData mode, bool closeAfterSelect)
        {
            SelectedMode = mode;
            RefreshSelectedModeText();

            if (closeAfterSelect)
            {
                CloseModePanel();
            }
        }

        /// <summary>
        /// モード選択画面を開く。
        /// </summary>
        private void OpenModePanel()
        {
            _mainPanel.SetActive(false);
            _modelPanel.SetActive(false);

            _modePanel.SetActive(true);

            // UIの手前に表示する。
            _modePanel.transform.SetAsLastSibling();

            SelectUi(GetFirstModeSelectable());
        }

        /// <summary>
        /// モード選択画面を閉じて、通常ロビー画面に戻る。
        /// </summary>
        private void CloseModePanel()
        {
            _modePanel.SetActive(false);

            _mainPanel.SetActive(true);
            _modelPanel.SetActive(true);

            SelectUi(_firstMainSelectable);
        }

        /// <summary>
        /// MainPanel上の選択中モード名表示を更新する。
        /// </summary>
        private void RefreshSelectedModeText()
        {
            if (_selectedModeText == null)
            {
                return;
            }

            if (SelectedMode == null)
            {
                _selectedModeText.text = "未選択";
                return;
            }

            _selectedModeText.text = SelectedMode.DisplayName;
        }

        private Selectable GetFirstModeSelectable()
        {
            if (_modeButtonViews.Count <= 0)
            {
                return _closeButton;
            }

            return _modeButtonViews[0].Selectable;
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

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            if (_modePanel == null || !_modePanel.activeSelf)
            {
                return;
            }

            CloseModePanel();
        }
    }
}
