using UnityEngine;

namespace Uraty.Features.Button
{
    public enum ButtonNavigationDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// 十字キーでの移動先を指定するためのノード。
    /// </summary>
    public sealed class ButtonNavigationNode : MonoBehaviour
    {
        [SerializeField] private ButtonNavigationNode _upNode;
        [SerializeField] private ButtonNavigationNode _downNode;
        [SerializeField] private ButtonNavigationNode _leftNode;
        [SerializeField] private ButtonNavigationNode _rightNode;

#if UNITY_EDITOR
        [SerializeField] private Vector2 _editorPosition;
        public Vector2 EditorPosition
        {
            get => _editorPosition;
            set => _editorPosition = value;
        }
#endif

        public ButtonNavigationNode GetNode(ButtonNavigationDirection direction)
        {
            return direction switch
            {
                ButtonNavigationDirection.Up => _upNode,
                ButtonNavigationDirection.Down => _downNode,
                ButtonNavigationDirection.Left => _leftNode,
                ButtonNavigationDirection.Right => _rightNode,
                _ => null
            };
        }

        public void SetNode(ButtonNavigationDirection direction, ButtonNavigationNode node)
        {
            switch (direction)
            {
                case ButtonNavigationDirection.Up:
                    _upNode = node;
                    break;
                case ButtonNavigationDirection.Down:
                    _downNode = node;
                    break;
                case ButtonNavigationDirection.Left:
                    _leftNode = node;
                    break;
                case ButtonNavigationDirection.Right:
                    _rightNode = node;
                    break;
            }
        }

        public ButtonNavigationNode GetNextNode(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return direction.x > 0.0f ? _rightNode : _leftNode;
            }

            return direction.y > 0.0f ? _upNode : _downNode;
        }
    }
}
