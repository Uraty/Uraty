using UnityEngine;
using UnityEngine.UI;

namespace Uraty.Features.Scenes
{
    /// <summary>
    /// UI Buttonからシーン切り替えを要求するためのコンポーネント。
    ///
    /// Inspectorで遷移先の SceneId を指定し、クリック時に SceneFlowManager に要求を投げる。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class SceneChangeButton : MonoBehaviour
    {
        private SceneFlowManager _sceneFlowManager;

        [Header("Request")]
        [SerializeField] private SceneId _nextSceneId = SceneId.Title;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();

            // Inspector 未設定の場合はシーン上から探す（クリック時の利便性目的）
            if (_sceneFlowManager == null)
            {
                _sceneFlowManager = FindAnyObjectByType<SceneFlowManager>();
            }

            if (_button != null)
            {
                _button.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void OnDestroy()
        {
            //破棄時に購読解除（参照が残らないようにする）
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        private void HandleButtonClicked()
        {
            if (_sceneFlowManager == null)
            {
                Debug.LogWarning($"{nameof(SceneFlowManager)} is not assigned.");
                return;
            }

            if (_sceneFlowManager.IsLoading)
            {
                return;
            }

            _sceneFlowManager.RequestChange(_nextSceneId);
        }
    }
}
