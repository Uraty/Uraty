using UnityEngine;

using Uraty.Systems.Input;

namespace Uraty.Application.Result
{
    public class ResultSystem : MonoBehaviour
    {
        [SerializeField] private GameInput _gameInput;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Awake()
        {
            if (_gameInput == null)
            {
                Debug.LogError("_gameInputの設定がされていません");
            }

            _gameInput.EnableUIInput();
        }
    }
}
