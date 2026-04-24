using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Player
{
    public sealed class PlayerMove : MonoBehaviour
    {
        private const float DefaultMoveSpeedMetersPerSecond = 5.0f;

        [SerializeField] private RoleDefinition _roleDefinition;


        [Header("地形を考慮した移動解決")]
        [SerializeField] private PlayerTerrainMoveResolver _terrainMoveResolver;

        private float _moveSpeedMetersPerSecond = DefaultMoveSpeedMetersPerSecond;

        private InputAction _moveAction;
        private Vector3 _moveInput;

        private void Awake()
        {
            _moveAction = new InputAction(name: "Move", type: InputActionType.Value);

            if (_roleDefinition != null)
            {
                _moveSpeedMetersPerSecond = _roleDefinition.MoveSpeed;
            }
            else
            {
                Debug.LogWarning($"RoleDefinition is not assigned. Using default move speed: {_moveSpeedMetersPerSecond} m/s");
            }

            _ = _moveAction.AddCompositeBinding("3DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");

            _ = _moveAction.AddBinding("<Gamepad>/leftStick");

            if (_terrainMoveResolver == null)
            {
                _terrainMoveResolver = GetComponent<PlayerTerrainMoveResolver>();
            }
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
            Vector3 desiredMoveDelta = moveDirection * _moveSpeedMetersPerSecond * Time.deltaTime;

            Vector3 resolvedMoveDelta = _terrainMoveResolver != null
                ? _terrainMoveResolver.ResolveMoveDelta(transform.position, desiredMoveDelta)
                : desiredMoveDelta;

            transform.position += resolvedMoveDelta;
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
        }
    }
}
