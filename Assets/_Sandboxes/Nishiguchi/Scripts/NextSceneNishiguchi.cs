using UnityEngine;
using UnityEngine.SceneManagement;

using Uraty.Application.Matching;
using Uraty.Features.Button;

public class NextSceneNishiguchi : MonoBehaviour
{
    [SerializeField] private ButtonSystem _buttonSystem;
    [SerializeField] private string _nextSceneName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (_buttonSystem == null)
        {
            Debug.LogError($"{nameof(MatchingCancel)}: GameInputが設定されていません。");
            return;
        }

        _buttonSystem.AddPressedListener(OnClickNextSceneButton);
    }

    public void OnClickNextSceneButton()
    {
        SceneManager.LoadScene(_nextSceneName);
    }

}
