using UnityEngine;

namespace Uraty.System.Input
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameInput : MonoBehaviour
    {
        private GameInputActions _inputActions;

        public GameInputActions.PlayerActions Player => _inputActions.Player;
        public GameInputActions.UIActions UI => _inputActions.UI;

        private void Awake()
        {
            _inputActions = new GameInputActions();
        }

        private void OnDisable()
        {
            DisableAllInput();
        }

        private void OnDestroy()
        {
            _inputActions?.Dispose();
        }

        public void EnableGameplayInput()
        {
            _inputActions.UI.Disable();
            _inputActions.Player.Enable();
        }

        public void EnableUIInput()
        {
            _inputActions.Player.Disable();
            _inputActions.UI.Enable();
        }

        public void EnableGameplayAndUIInput()
        {
            _inputActions.Player.Enable();
            _inputActions.UI.Enable();
        }

        public void DisableAllInput()
        {
            _inputActions.Player.Disable();
            _inputActions.UI.Disable();
        }
    }
}
