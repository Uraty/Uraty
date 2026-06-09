using UnityEngine;
using UnityEngine.UI;

namespace Uraty.Application.Matching
{
    public class MoveIcon : MonoBehaviour
    {
        [SerializeField] private Image _gameBackGround;
        [SerializeField] private RectTransform _gameIcon;
        [SerializeField] private Image _gameIconImage;
        [SerializeField] private Image _annotation;
        [SerializeField] private Color[] _backGroundColor;
        [SerializeField] private Sprite[] _gameIconSprites;
        [SerializeField] private float _moveSpeed = 5.0f;

        private int _iconNumber;

        private void Awake()
        {
            ChangeRandomIcon();
        }

        private void Update()
        {
            Vector3 direction = new Vector3(1.0f, 1.0f, 0.0f).normalized;
            _gameIcon.position += direction * _moveSpeed * Time.deltaTime;
        }

        private void ChangeRandomIcon()
        {
            if (_gameIconImage == null || _gameIconSprites.Length == 0)
            {
                return;
            }

            _iconNumber = Random.Range(0, _gameIconSprites.Length);
            _gameBackGround.color = _backGroundColor[_iconNumber];
            _annotation.color = new Color(
                _backGroundColor[_iconNumber].r,
                _backGroundColor[_iconNumber].g,
                _backGroundColor[_iconNumber].b,
                _annotation.color.a
            );
            _gameIconImage.sprite = _gameIconSprites[_iconNumber];
        }
    }
}
