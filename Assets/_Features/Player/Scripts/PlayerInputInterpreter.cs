using UnityEngine;
using UnityEngine.InputSystem;

using Uraty.Systems.Input;

namespace Uraty.Features.Player
{
    /// <summary>
    /// InputActions の生入力を、ゲーム側で使いやすい形に変換して保持するクラス。
    /// AutoAim かどうかは判断しない。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputInterpreter : MonoBehaviour
    {
        private enum AimInputSource
        {
            None,
            Gamepad,
            Mouse,
        }

        private const float MinInputSqrMagnitude = 0.0001f;

        [Header("Input Actions")]
        [SerializeField] private GameInput _input;

        [Header("References")]
        [SerializeField] private Camera _targetCamera;

        [Header("Mouse Virtual Stick Aim")]
        [Tooltip("マウス仮想スティックの最大半径。小さいほど素早く反転でき、大きいほど細かく狙いやすい")]
        [Min(1f)]
        [SerializeField] private float _mouseVirtualStickOuterRadiusPixels = 120f;

        [Tooltip("マウス仮想スティックの中央デッドゾーン半径。この範囲内は Aim していない扱い")]
        [Min(0f)]
        [SerializeField] private float _mouseVirtualStickDeadZonePixels = 24f;

        [Header("Gamepad Aim")]
        [Tooltip("右スティックで地面上オフセットを動かす速さ（距離/秒）")]
        [Min(0f)]
        [SerializeField] private float _gamepadAimMoveSpeedUnitsPerSecond = 10f;

        [Tooltip("右スティック移動用デッドゾーン")]
        [Range(0f, 1f)]
        [SerializeField] private float _gamepadAimMoveDeadZone = 0.20f;

        [Header("Shared Aim Offset")]
        [Tooltip("プレイヤー基準の地面上オフセット最大長")]
        [Min(0f)]
        [SerializeField] private float _maxAimOffsetDistance = 15f;

        private InputAction MoveAction => _input?.Player.Move;
        private InputAction AimAction => _input?.Player.Aim;
        private InputAction AttackAction => _input?.Player.Attack;
        private InputAction SuperAction => _input?.Player.Super;

        private Vector2 _rawMoveInput;
        private Vector2 _rawAimInput;

        /// <summary>
        /// Mouse 用の仮想スティック位置。
        /// 単位は pixel。
        /// </summary>
        private Vector2 _mouseVirtualStickOffsetPixels = Vector2.zero;

        /// <summary>
        /// プレイヤー位置からの地面上オフセット。
        /// Y は常に使わない。
        /// </summary>
        private Vector3 _aimOffsetWorldFromPlayer = Vector3.zero;

        private Vector3 _moveDirectionWorld;
        private Vector3 _aimDirectionWorld;
        private Vector3 _aimPointWorld;

        private AimInputSource _currentAimSource = AimInputSource.None;

        private bool _attackPressedThisFrame;
        private bool _attackIsPressed;
        private bool _attackReleasedThisFrame;

        private bool _superPressedThisFrame;
        private bool _superIsPressed;
        private bool _superReleasedThisFrame;

        public Vector3 MoveDirectionWorld => _moveDirectionWorld;

        /// <summary>
        /// 現在の Aim 方向。
        /// Aim 入力がない場合は Vector3.zero。
        /// </summary>
        public Vector3 AimDirectionWorld => _aimDirectionWorld;

        /// <summary>
        /// AimDirectionWorld が Vector3.zero の場合、この値は transform.position と同じ扱い。
        /// </summary>
        public Vector3 AimPointWorld => _aimPointWorld;

        /// <summary>
        /// AimPointWorld を画面へ投影した座標。
        /// UI 側で照準位置が必要な場合に使う。
        /// </summary>
        public Vector2 CurrentAimScreenPosition => GetCurrentAimScreenPosition();

        public bool AttackPressedThisFrame => _attackPressedThisFrame;
        public bool AttackIsPressed => _attackIsPressed;
        public bool AttackReleasedThisFrame => _attackReleasedThisFrame;

        public bool SuperPressedThisFrame => _superPressedThisFrame;
        public bool SuperIsPressed => _superIsPressed;
        public bool SuperReleasedThisFrame => _superReleasedThisFrame;

        private void OnValidate()
        {
            _mouseVirtualStickOuterRadiusPixels = Mathf.Max(1f, _mouseVirtualStickOuterRadiusPixels);
            _mouseVirtualStickDeadZonePixels = Mathf.Max(0f, _mouseVirtualStickDeadZonePixels);

            if (_mouseVirtualStickDeadZonePixels > _mouseVirtualStickOuterRadiusPixels)
            {
                _mouseVirtualStickDeadZonePixels = _mouseVirtualStickOuterRadiusPixels;
            }

            _gamepadAimMoveDeadZone = Mathf.Clamp01(_gamepadAimMoveDeadZone);
            _gamepadAimMoveSpeedUnitsPerSecond = Mathf.Max(0f, _gamepadAimMoveSpeedUnitsPerSecond);
            _maxAimOffsetDistance = Mathf.Max(0f, _maxAimOffsetDistance);
        }

        private void Awake()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            _mouseVirtualStickOffsetPixels = Vector2.zero;
            _aimOffsetWorldFromPlayer = Vector3.zero;
            _aimPointWorld = transform.position;
            _aimDirectionWorld = Vector3.zero;
        }

        private void OnEnable()
        {
            RefreshInputs();
            RefreshButtonStates();
            RefreshAimSource();
            ResetAimContextIfActionStarted();
            UpdateAimOffsetFromCurrentInput();
            RefreshConvertedValues();
        }

        private void Update()
        {
            RefreshInputs();
            RefreshButtonStates();
            RefreshAimSource();
            ResetAimContextIfActionStarted();
            UpdateAimOffsetFromCurrentInput();
            RefreshConvertedValues();
        }

        private void RefreshInputs()
        {
            _rawMoveInput = MoveAction != null
                ? MoveAction.ReadValue<Vector2>()
                : Vector2.zero;

            _rawAimInput = AimAction != null
                ? AimAction.ReadValue<Vector2>()
                : Vector2.zero;
        }

        private void RefreshButtonStates()
        {
            _attackPressedThisFrame = IsPressedThisFrame(AttackAction);
            _attackIsPressed = IsPressed(AttackAction);
            _attackReleasedThisFrame = IsReleasedThisFrame(AttackAction);

            _superPressedThisFrame = IsPressedThisFrame(SuperAction);
            _superIsPressed = IsPressed(SuperAction);
            _superReleasedThisFrame = IsReleasedThisFrame(SuperAction);
        }

        private void RefreshAimSource()
        {
            if (AimAction != null && AimAction.activeControl != null && AimAction.activeControl.device != null)
            {
                if (AimAction.activeControl.device is Gamepad)
                {
                    _currentAimSource = AimInputSource.Gamepad;
                    return;
                }

                if (AimAction.activeControl.device is Mouse)
                {
                    _currentAimSource = AimInputSource.Mouse;
                    return;
                }
            }

            AimInputSource attackSource = GetActionSourceThisFrame(AttackAction);
            if (attackSource != AimInputSource.None)
            {
                _currentAimSource = attackSource;
                return;
            }

            AimInputSource superSource = GetActionSourceThisFrame(SuperAction);
            if (superSource != AimInputSource.None)
            {
                _currentAimSource = superSource;
            }
        }

        private void ResetAimContextIfActionStarted()
        {
            if (!_attackPressedThisFrame && !_superPressedThisFrame)
            {
                return;
            }

            _aimOffsetWorldFromPlayer = Vector3.zero;
            _mouseVirtualStickOffsetPixels = Vector2.zero;
        }

        private void UpdateAimOffsetFromCurrentInput()
        {
            if (!ShouldKeepAimThisFrame())
            {
                ClearAim();
                return;
            }

            if (!IsAnyAimButtonPressed())
            {
                return;
            }

            if (!TryGetFlatCameraBasis(out Vector3 flatRight, out Vector3 flatForward))
            {
                ClearAim();
                return;
            }

            switch (_currentAimSource)
            {
                case AimInputSource.Mouse:
                    UpdateMouseVirtualStickAimOffset(flatRight, flatForward);
                    break;

                case AimInputSource.Gamepad:
                    UpdateGamepadAimOffset(flatRight, flatForward);
                    break;

                default:
                    ClearAim();
                    break;
            }

            ClampAimOffset();
        }

        /// <summary>
        /// Mouse Delta を仮想スティックとして扱い、Aim オフセットへ変換する。
        /// </summary>
        private void UpdateMouseVirtualStickAimOffset(Vector3 flatRight, Vector3 flatForward)
        {
            _mouseVirtualStickOffsetPixels += _rawAimInput;

            float outerRadius = Mathf.Max(1f, _mouseVirtualStickOuterRadiusPixels);
            float deadZone = Mathf.Clamp(_mouseVirtualStickDeadZonePixels, 0f, outerRadius);

            float stickMagnitude = _mouseVirtualStickOffsetPixels.magnitude;

            if (stickMagnitude > outerRadius)
            {
                _mouseVirtualStickOffsetPixels = _mouseVirtualStickOffsetPixels.normalized * outerRadius;
                stickMagnitude = outerRadius;
            }

            if (stickMagnitude <= deadZone)
            {
                _aimOffsetWorldFromPlayer = Vector3.zero;
                return;
            }

            Vector2 stickDirection = _mouseVirtualStickOffsetPixels.normalized;

            Vector3 directionWorld =
                flatRight * stickDirection.x +
                flatForward * stickDirection.y;

            directionWorld.y = 0f;

            if (directionWorld.sqrMagnitude <= MinInputSqrMagnitude)
            {
                _aimOffsetWorldFromPlayer = Vector3.zero;
                return;
            }

            float rangeRatio = Mathf.InverseLerp(deadZone, outerRadius, stickMagnitude);
            float aimDistance = Mathf.Max(0f, _maxAimOffsetDistance) * rangeRatio;

            _aimOffsetWorldFromPlayer = directionWorld.normalized * aimDistance;
        }

        private void UpdateGamepadAimOffset(Vector3 flatRight, Vector3 flatForward)
        {
            Vector2 processedStick = ApplyRadialDeadZone(_rawAimInput, _gamepadAimMoveDeadZone);

            Vector3 deltaWorld =
                flatRight * (processedStick.x * _gamepadAimMoveSpeedUnitsPerSecond * Time.deltaTime) +
                flatForward * (processedStick.y * _gamepadAimMoveSpeedUnitsPerSecond * Time.deltaTime);

            _aimOffsetWorldFromPlayer += deltaWorld;
        }

        private void ClampAimOffset()
        {
            _aimOffsetWorldFromPlayer.y = 0f;

            float maxLength = Mathf.Max(0f, _maxAimOffsetDistance);
            if (maxLength <= 0f)
            {
                _aimOffsetWorldFromPlayer = Vector3.zero;
                return;
            }

            Vector3 flatOffset = _aimOffsetWorldFromPlayer;
            flatOffset.y = 0f;

            if (flatOffset.magnitude > maxLength)
            {
                _aimOffsetWorldFromPlayer = flatOffset.normalized * maxLength;
            }
        }

        private void RefreshConvertedValues()
        {
            _moveDirectionWorld = ConvertInputToCameraRelativeDirection(_rawMoveInput);

            if (TryBuildAimPointAndDirection(out Vector3 aimPointWorld, out Vector3 aimDirectionWorld))
            {
                _aimPointWorld = aimPointWorld;
                _aimDirectionWorld = aimDirectionWorld;
            }
            else
            {
                _aimPointWorld = transform.position;
                _aimDirectionWorld = Vector3.zero;
            }
        }

        private bool TryBuildAimPointAndDirection(out Vector3 aimPointWorld, out Vector3 aimDirectionWorld)
        {
            aimPointWorld = Vector3.zero;
            aimDirectionWorld = Vector3.zero;

            Vector3 offset = _aimOffsetWorldFromPlayer;
            offset.y = 0f;

            if (offset.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return false;
            }

            aimPointWorld = transform.position + offset;
            aimPointWorld.y = transform.position.y;

            aimDirectionWorld = offset.normalized;
            return true;
        }

        private Vector3 ConvertInputToCameraRelativeDirection(Vector2 input)
        {
            if (!TryGetFlatCameraBasis(out Vector3 flatRight, out Vector3 flatForward))
            {
                return Vector3.zero;
            }

            if (input.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return Vector3.zero;
            }

            Vector3 direction = (flatRight * input.x) + (flatForward * input.y);
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return Vector3.zero;
            }

            return direction.normalized;
        }

        private bool TryGetFlatCameraBasis(out Vector3 flatRight, out Vector3 flatForward)
        {
            flatRight = Vector3.zero;
            flatForward = Vector3.zero;

            if (_targetCamera == null)
            {
                return false;
            }

            flatRight = _targetCamera.transform.right;
            flatForward = _targetCamera.transform.forward;

            flatRight.y = 0f;
            flatForward.y = 0f;

            if (flatRight.sqrMagnitude <= MinInputSqrMagnitude ||
                flatForward.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return false;
            }

            flatRight.Normalize();
            flatForward.Normalize();
            return true;
        }

        private Vector2 GetCurrentAimScreenPosition()
        {
            if (_targetCamera == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 world = _aimDirectionWorld.sqrMagnitude > MinInputSqrMagnitude
                ? _aimPointWorld
                : transform.position;

            Vector3 screen = _targetCamera.WorldToScreenPoint(world);

            return new Vector2(screen.x, screen.y);
        }

        private bool ShouldKeepAimThisFrame()
        {
            return IsAnyAimButtonPressed() ||
                   _attackReleasedThisFrame ||
                   _superReleasedThisFrame;
        }

        private bool IsAnyAimButtonPressed()
        {
            return _attackIsPressed || _superIsPressed;
        }

        private void ClearAim()
        {
            _aimOffsetWorldFromPlayer = Vector3.zero;
            _mouseVirtualStickOffsetPixels = Vector2.zero;
        }

        private bool IsPressedThisFrame(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }

        private bool IsPressed(InputAction action)
        {
            return action != null && action.IsPressed();
        }

        private bool IsReleasedThisFrame(InputAction action)
        {
            return action != null && action.WasReleasedThisFrame();
        }

        private AimInputSource GetActionSourceThisFrame(InputAction action)
        {
            if (action == null || !action.WasPressedThisFrame())
            {
                return AimInputSource.None;
            }

            if (action.activeControl == null || action.activeControl.device == null)
            {
                return AimInputSource.None;
            }

            if (action.activeControl.device is Mouse)
            {
                return AimInputSource.Mouse;
            }

            if (action.activeControl.device is Gamepad)
            {
                return AimInputSource.Gamepad;
            }

            return AimInputSource.None;
        }

        private Vector2 ApplyRadialDeadZone(Vector2 input, float deadZone)
        {
            float magnitude = input.magnitude;

            if (magnitude <= deadZone)
            {
                return Vector2.zero;
            }

            float normalizedMagnitude = Mathf.InverseLerp(deadZone, 1f, Mathf.Clamp01(magnitude));
            return input.normalized * normalizedMagnitude;
        }
    }
}
