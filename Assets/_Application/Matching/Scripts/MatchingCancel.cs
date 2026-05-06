using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Uraty.Systems.Input;

namespace Uraty.Application.Matching
{
    public sealed class MatchingCancel : MonoBehaviour
    {
        [SerializeField] private GameInput _gameInput;

        [Header("遷移先Scene名")]
        [SerializeField] private string _targetSceneName = "";

        [Header("遷移方式")]
        [SerializeField] private LoadSceneMode _loadSceneMode = LoadSceneMode.Single;

        private Button _cancelButton;

        private void Awake()
        {
            _cancelButton = GetComponent<Button>();
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _gameInput.EnableUIInput();
        }

        // Update is called once per frame
        void Update()
        {

        }
        private void OnEnable()
        {
            _cancelButton.onClick.AddListener(HandleButtonPressed);
        }

        private void OnDisable()
        {
            _cancelButton.onClick.RemoveListener(HandleButtonPressed);
        }

        /// <summary>
        /// ボタンが押された時に、指定したSceneをロードする。
        /// </summary>
        private void HandleButtonPressed()
        {
            if (string.IsNullOrWhiteSpace(_targetSceneName))
            {
                Debug.LogError($"{nameof(MatchingCancel)}: 遷移先Scene名が設定されていません。");
                return;
            }

            SceneManager.LoadScene(_targetSceneName, _loadSceneMode);
        }
    }
}
