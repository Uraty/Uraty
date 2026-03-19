using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Player
{
    public sealed class PlayerMove : MonoBehaviour
    {
        private const float DefaultMoveSpeedMetersPerSecond = 5.0f;

        // 移動スピード
        [SerializeField] private float _moveSpeedMetersPerSecond = DefaultMoveSpeedMetersPerSecond;

        private InputAction _moveAction;
        private Vector3 _moveInput;

        private void Awake()
        {
            _moveAction = new InputAction(name: "Move", type: InputActionType.Value);

            _ = _moveAction.AddCompositeBinding("3DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _ = _moveAction.AddBinding("<Gamepad>/leftStick");
        }

        private void OnEnable()
        {
            _moveAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.Disable();
        }

        private void Update()
        {
            _moveInput = Vector3.ClampMagnitude(_moveAction.ReadValue<Vector3>(), 1.0f);

            var moveDirection = new Vector3(_moveInput.x, 0.0f, _moveInput.y);
            transform.position += moveDirection * _moveSpeedMetersPerSecond * Time.deltaTime;
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
        }
    }
}
