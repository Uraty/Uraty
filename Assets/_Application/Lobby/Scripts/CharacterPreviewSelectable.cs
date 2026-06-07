using UnityEngine;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// キャラ選択画面で表示される3Dキャラの選択状態を管理するクラス。
    /// </summary>
    public sealed class CharacterPreviewSelectable : MonoBehaviour
    {
        // 選択されていない通常時のスケール。
        private Vector3 _defaultScale;

        public int Index
        {
            get; private set;
        }

        private void Awake()
        {
            _defaultScale = transform.localScale;
        }

        public void Initialize(int index)
        {
            Index = index;
            _defaultScale = transform.localScale;
        }
    }
}
