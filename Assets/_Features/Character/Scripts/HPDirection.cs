using System.Collections;

using TMPro;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class HPDirection : MonoBehaviour
    {
        private const float MinAmount = 0.0001f;

        [Header("Text Root")]
        [SerializeField] private RectTransform _textRoot;
        [SerializeField] private TextMeshProUGUI _textPrefab;
        [SerializeField] private Vector2 _spawnAnchoredPosition;
        [SerializeField] private Vector2 _textSize = new Vector2(160.0f, 60.0f);

        [Header("Damage Text")]
        [SerializeField] private Color _damageTextColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);
        [SerializeField] private float _damageFontSize = 36.0f;

        [Header("Heal Text")]
        [SerializeField] private Color _healTextColor = new Color(0.25f, 1.0f, 0.45f, 1.0f);
        [SerializeField] private float _healFontSize = 36.0f;

        [Header("Display")]
        [SerializeField] private float _destroyDelaySeconds = 1.0f;
        [SerializeField] private bool _isMoveEnabled = true;
        [SerializeField] private Vector2 _moveOffset = new Vector2(0.0f, 40.0f);
        [SerializeField] private float _moveDurationSeconds = 0.5f;

        public void ShowDamage(float damageAmount)
        {
            SpawnHPText(damageAmount, _damageTextColor, _damageFontSize);
        }

        public void ShowHeal(float healAmount)
        {
            SpawnHPText(healAmount, _healTextColor, _healFontSize);
        }

        private void Awake()
        {
            CacheTextRoot();
        }

        private void OnValidate()
        {
            CacheTextRoot();
        }

        private void CacheTextRoot()
        {
            if (_textRoot != null)
            {
                return;
            }

            _textRoot = transform as RectTransform;
        }

        private void SpawnHPText(
            float amount,
            Color textColor,
            float fontSize)
        {
            float absoluteAmount = Mathf.Abs(amount);

            if (absoluteAmount <= MinAmount)
            {
                return;
            }

            TextMeshProUGUI hpText = CreateHPText();

            if (hpText == null)
            {
                return;
            }

            int displayAmount = Mathf.Max(1, Mathf.CeilToInt(absoluteAmount));
            hpText.text = $"{displayAmount}";
            hpText.color = textColor;
            hpText.fontSize = fontSize;
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.enableWordWrapping = false;
            hpText.raycastTarget = false;

            RectTransform textRectTransform = hpText.rectTransform;
            textRectTransform.anchoredPosition = _spawnAnchoredPosition;
            textRectTransform.SetAsLastSibling();

            if (_isMoveEnabled && _moveDurationSeconds > 0.0f)
            {
                StartCoroutine(MoveText(textRectTransform));
            }

            if (_destroyDelaySeconds > 0.0f)
            {
                Destroy(hpText.gameObject, _destroyDelaySeconds);
            }
        }

        private TextMeshProUGUI CreateHPText()
        {
            Transform parentTransform = GetParentTransform();

            if (_textPrefab != null)
            {
                return Instantiate(_textPrefab, parentTransform, false);
            }

            var textObject = new GameObject(
                "HPChangeText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(parentTransform, false);

            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = _textSize;

            return textObject.GetComponent<TextMeshProUGUI>();
        }

        private Transform GetParentTransform()
        {
            if (_textRoot != null)
            {
                return _textRoot;
            }

            return transform;
        }

        private IEnumerator MoveText(RectTransform textRectTransform)
        {
            Vector2 startPosition = textRectTransform.anchoredPosition;
            Vector2 endPosition = startPosition + _moveOffset;
            float elapsedSeconds = 0.0f;

            while (elapsedSeconds < _moveDurationSeconds)
            {
                if (textRectTransform == null)
                {
                    yield break;
                }

                elapsedSeconds += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsedSeconds / _moveDurationSeconds);
                textRectTransform.anchoredPosition = Vector2.Lerp(
                    startPosition,
                    endPosition,
                    ratio);

                yield return null;
            }

            if (textRectTransform != null)
            {
                textRectTransform.anchoredPosition = endPosition;
            }
        }
    }
}
