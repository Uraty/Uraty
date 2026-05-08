using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Uraty.Feature.Akane_GameMode;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// モード選択画面に表示される、モード1つ分のボタンView。
    /// GameModeDataの表示情報をUIへ反映し、押されたら親Controllerへ通知する。
    /// </summary>
    public sealed class ModeButtonView : MonoBehaviour
    {
        // このView自身のButton。
        [SerializeField] private Button _button;

        // モードアイコン表示用Image。
        [SerializeField] private Image _iconImage;

        // モード名表示用Text。
        [SerializeField] private TextMeshProUGUI _nameText;

        // モード説明表示用Text。
        [SerializeField] private TextMeshProUGUI _descriptionText;

        // このボタンに対応するモードデータ。
        private GameModeData _modeData;

        // ボタンが押されたときに呼ぶ処理。
        // 親のLobbyModeSelectControllerから渡される。
        private Action<GameModeData> _onClicked;

        /// <summary>
        /// モードボタンの初期化。
        /// 表示内容を更新し、クリック時の通知先を登録する。
        /// </summary>
        public void Initialize(GameModeData modeData, Action<GameModeData> onClicked)
        {
            _modeData = modeData;
            _onClicked = onClicked;

            if (_nameText != null)
            {
                _nameText.text = modeData.DisplayName;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = modeData.Description;
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = modeData.Icon;

                // アイコンが未設定ならImageを非表示にする。
                _iconImage.enabled = modeData.Icon != null;
            }

            // 古いListenerが残らないように消してから登録する。
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(OnClick);
        }

        /// <summary>
        /// ボタン押下時に、対応するモードデータを親Controllerへ渡す。
        /// </summary>
        private void OnClick()
        {
            _onClicked?.Invoke(_modeData);
        }
    }
}
