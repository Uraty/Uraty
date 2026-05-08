using UnityEngine;

using Uraty.Feature.Akane_TestCharacter;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// キャラ選択画面で表示される3Dキャラの選択状態を管理するクラス。
    /// どのCharacterDataに対応しているかを保持し、選択中なら見た目を少し大きくする。
    /// </summary>
    public sealed class CharacterPreviewSelectable : MonoBehaviour
    {
        // 選択されていない通常時のスケール。
        private Vector3 _defaultScale;

        /// <summary>
        /// この表示キャラに対応するキャラデータ。
        /// </summary>
        public CharacterData Character
        {
            get; private set;
        }

        /// <summary>
        /// キャラ生成後に、対応するCharacterDataを登録する。
        /// </summary>
        public void Initialize(CharacterData character)
        {
            Character = character;

            // 選択状態を戻すために、初期スケールを保存しておく。
            _defaultScale = transform.localScale;
        }

        /// <summary>
        /// 選択状態に応じて見た目を変える。
        /// 現状は選択中なら少し大きくする。
        /// </summary>
        public void SetSelected(bool isSelected, float selectedScale)
        {
            transform.localScale = isSelected
                ? _defaultScale * selectedScale
                : _defaultScale;
        }
    }
}
