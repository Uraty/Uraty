using UnityEngine;

using Uraty.Systems.Input;

namespace Uraty.Feature.Button
{
    public class ButtonSystem : MonoBehaviour
    {
        [SerializeField] private GameInput _gameInput;
        [SerializeField] private bool _isUseButtonSubmit = false;
        [SerializeField] private bool _isUseButtonCancel = false;
        [SerializeField] private bool _isUseButtonPoint = false;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _gameInput.EnableUIInput();

        }

        // Update is called once per frame
        void Update()
        {

        }

    }

}
