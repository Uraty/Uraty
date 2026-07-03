using TMPro;

using UnityEngine;

namespace Uraty.Feature.TemplateText
{
    public sealed class CreateTips : MonoBehaviour
    {
        [SerializeField] private TMP_Text _targetText;

        [SerializeField, TextArea(2, 4)]
        private string[] _templateTexts;

        [SerializeField] private bool _displayOnStart = true;

        public string CurrentTemplateText { get; private set; } = string.Empty;

        private void Awake()
        {
            if (_targetText == null)
            {
                _targetText = GetComponent<TMP_Text>();
            }
        }

        private void Start()
        {
            if (_displayOnStart)
            {
                DisplayRandomTemplateText();
            }
        }

        public void DisplayRandomTemplateText()
        {
            if (_targetText == null)
            {
                Debug.LogWarning($"{nameof(CreateTips)}: 表示先のTMP_Textが設定されていません。", this);
                return;
            }

            if (!TryGetRandomTemplateText(out string templateText))
            {
                Debug.LogWarning($"{nameof(CreateTips)}: 有効なテンプレートテキストが登録されていません。", this);

                CurrentTemplateText = string.Empty;
                _targetText.text = string.Empty;
                return;
            }

            CurrentTemplateText = templateText;
            _targetText.text = CurrentTemplateText;
        }

        private bool TryGetRandomTemplateText(out string templateText)
        {
            templateText = string.Empty;

            if (_templateTexts == null || _templateTexts.Length == 0)
            {
                return false;
            }

            int validTemplateCount = 0;

            for (int i = 0; i < _templateTexts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(_templateTexts[i]))
                {
                    validTemplateCount++;
                }
            }

            if (validTemplateCount == 0)
            {
                return false;
            }

            int selectedTemplateOrder = Random.Range(0, validTemplateCount);

            for (int i = 0; i < _templateTexts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_templateTexts[i]))
                {
                    continue;
                }

                if (selectedTemplateOrder == 0)
                {
                    templateText = _templateTexts[i];
                    return true;
                }

                selectedTemplateOrder--;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_targetText == null)
            {
                _targetText = GetComponent<TMP_Text>();
            }
        }
#endif
    }
}
