using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Player
{
    /// <summary>
    /// InputActions の生入力を、ゲーム側で使いやすい形に変換して保持するクラス。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputInterpreter : MonoBehaviour
    {
        public enum AimInputSource
        {
            None,
            Gamepad,
            Mouse,
        }

        public enum ActionInputSource
        {
            None,
            Gamepad,
            Mouse,
        }

        public struct ReleaseInfo
        {
            public bool CanAutoAim;
            public Vector3 AimDirection;

            public ReleaseInfo(bool canAutoAim, Vector3 aimDirection)
            {
                CanAutoAim = canAutoAim;
                AimDirection = aimDirection;
            }
        }

        private const float MinInputSqrMagnitude = 0.0001f;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference _moveActionReference;
        [SerializeField] private InputActionReference _aimActionReference;
        [SerializeField] private InputActionReference _attackActionReference;
        [SerializeField] private InputActionReference _superActionReference;

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

        [Header("Auto Aim")]
        [Tooltip("押した時と離した時のオフセット差がこの距離以内なら『動かしていない』とみなす")]
        [Min(0f)]
        [SerializeField] private float _sameAimOffsetDistanceThreshold = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLog = false;

        private InputAction MoveAction => _moveActionReference != null ? _moveActionReference.action : null;
        private InputAction AimAction => _aimActionReference != null ? _aimActionReference.action : null;
        private InputAction AttackAction => _attackActionReference != null ? _attackActionReference.action : null;
        private InputAction SuperAction => _superActionReference != null ? _superActionReference.action : null;

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
        private Vector3 _lastNonZeroAimDirectionWorld = Vector3.forward;

        private bool _hasValidAimPointWorld;
        private Vector3 _aimPointWorld;

        private AimInputSource _currentAimSource = AimInputSource.None;

        private bool _hasPendingAttackRelease;
        private bool _hasPendingSuperRelease;

        private ReleaseInfo _pendingAttackRelease;
        private ReleaseInfo _pendingSuperRelease;

        private ActionInputSource _attackPressSource = ActionInputSource.None;
        private ActionInputSource _superPressSource = ActionInputSource.None;

        private Vector3 _attackPressAimOffsetWorldFromPlayer = Vector3.zero;
        private Vector3 _superPressAimOffsetWorldFromPlayer = Vector3.zero;

        public Vector3 MoveDirectionWorld => _moveDirectionWorld;
        public Vector3 AimDirectionWorld => _aimDirectionWorld;
        public Vector3 LastNonZeroAimDirectionWorld => _lastNonZeroAimDirectionWorld;

        public bool HasValidAimPointWorld => _hasValidAimPointWorld;
        public Vector3 AimPointWorld => _aimPointWorld;

        public AimInputSource CurrentAimSource => _currentAimSource;
        public bool IsAimFromGamepad => _currentAimSource == AimInputSource.Gamepad;
        public bool IsAimFromMouse => _currentAimSource == AimInputSource.Mouse;

        /// <summary>
        /// AimPointWorld を画面へ投影した座標。
        /// UI 側で照準位置が必要な場合に使う。
        /// </summary>
        public Vector2 CurrentAimScreenPosition => GetCurrentAimScreenPosition();

        public bool AttackPressedThisFrame => AttackAction != null && AttackAction.WasPressedThisFrame();
        public bool AttackIsPressed => AttackAction != null && AttackAction.IsPressed();
        public bool AttackReleasedThisFrame => AttackAction != null && AttackAction.WasReleasedThisFrame();

        public bool SuperPressedThisFrame => SuperAction != null && SuperAction.WasPressedThisFrame();
        public bool SuperIsPressed => SuperAction != null && SuperAction.IsPressed();
        public bool SuperReleasedThisFrame => SuperAction != null && SuperAction.WasReleasedThisFrame();

        public ActionInputSource AttackPressedSourceThisFrame => GetPressSourceThisFrame(AttackAction);
        public ActionInputSource SuperPressedSourceThisFrame => GetPressSourceThisFrame(SuperAction);

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
            _sameAimOffsetDistanceThreshold = Mathf.Max(0f, _sameAimOffsetDistanceThreshold);
        }

        private void Awake()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            _mouseVirtualStickOffsetPixels = Vector2.zero;
            _aimOffsetWorldFromPlayer = Vector3.zero;

            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > MinInputSqrMagnitude)
            {
                _lastNonZeroAimDirectionWorld = flatForward.normalized;
            }
        }

        private void OnEnable()
        {
            RefreshInputs();
            RefreshAimSource();
            CachePressContext();
            UpdateAimOffsetFromCurrentInput();
            RefreshConvertedValues();
        }
        private void Update()
        {
            RefreshInputs();
            RefreshAimSource();
            CachePressContext();
            UpdateAimOffsetFromCurrentInput();
            RefreshConvertedValues();
            DetectReleaseTriggers();

            if (_enableDebugLog && IsAnyAimButtonPressed())
            {
                Debug.Log(
                    $"[PlayerInputInterpreter] source={_currentAimSource}, " +
                    $"rawAim={_rawAimInput}, " +
                    $"mouseVirtualStick={_mouseVirtualStickOffsetPixels}, " +
                    $"offsetWorld={_aimOffsetWorldFromPlayer}, " +
                    $"aimPointWorld={_aimPointWorld}, " +
                    $"aimDir={_aimDirectionWorld}");
            }
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

            ActionInputSource attackPressSource = AttackPressedSourceThisFrame;
            if (attackPressSource != ActionInputSource.None)
            {
                _currentAimSource = ConvertToAimInputSource(attackPressSource);
                return;
            }

            ActionInputSource superPressSource = SuperPressedSourceThisFrame;
            if (superPressSource != ActionInputSource.None)
            {
                _currentAimSource = ConvertToAimInputSource(superPressSource);
            }
        }

        private void CachePressContext()
        {
            if (AttackPressedThisFrame)
            {
                _attackPressSource = AttackPressedSourceThisFrame;
                ResetAimStartContext(_attackPressSource);
                _attackPressAimOffsetWorldFromPlayer = _aimOffsetWorldFromPlayer;
            }

            if (SuperPressedThisFrame)
            {
                _superPressSource = SuperPressedSourceThisFrame;
                ResetAimStartContext(_superPressSource);
                _superPressAimOffsetWorldFromPlayer = _aimOffsetWorldFromPlayer;
            }
        }

        private void ResetAimStartContext(ActionInputSource pressSource)
        {
            if (pressSource is not (ActionInputSource.Mouse or ActionInputSource.Gamepad))
            {
                return;
            }

            _aimOffsetWorldFromPlayer = Vector3.zero;
            _mouseVirtualStickOffsetPixels = Vector2.zero;
        }

        private void UpdateAimOffsetFromCurrentInput()
        {
            if (!IsAnyAimButtonPressed())
            {
                return;
            }

            if (!TryGetFlatCameraBasis(out Vector3 flatRight, out Vector3 flatForward))
            {
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
            }

            ClampAimOffset();
        }

        /// <summary>
        /// Mouse Delta を仮想スティックとして扱い、Aim オフセットへ変換する。
        ///
        /// 仮想スティックの方向を Aim 方向にし、
        /// 仮想スティックの中心からの距離を Aim 距離に変換する。
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
                _hasValidAimPointWorld = true;
                _aimPointWorld = aimPointWorld;
                _aimDirectionWorld = aimDirectionWorld;
                _lastNonZeroAimDirectionWorld = _aimDirectionWorld;
            }
            else
            {
                _hasValidAimPointWorld = false;
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

        private void DetectReleaseTriggers()
        {
            if (AttackReleasedThisFrame)
            {
                bool canAutoAim = !IsManualAimSpecified(
                    _attackPressSource,
                    _attackPressAimOffsetWorldFromPlayer);

                Vector3 aimDirection = GetResolvedAimDirection();

                _pendingAttackRelease = new ReleaseInfo(canAutoAim, aimDirection);
                _hasPendingAttackRelease = true;

                _attackPressSource = ActionInputSource.None;
            }

            if (SuperReleasedThisFrame)
            {
                bool canAutoAim = !IsManualAimSpecified(
                    _superPressSource,
                    _superPressAimOffsetWorldFromPlayer);

                Vector3 aimDirection = GetResolvedAimDirection();

                _pendingSuperRelease = new ReleaseInfo(canAutoAim, aimDirection);
                _hasPendingSuperRelease = true;

                _superPressSource = ActionInputSource.None;
            }
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

            Vector3 world = _hasValidAimPointWorld ? _aimPointWorld : transform.position;
            Vector3 screen = _targetCamera.WorldToScreenPoint(world);

            return new Vector2(screen.x, screen.y);
        }

        private bool IsAnyAimButtonPressed()
        {
            return AttackIsPressed || SuperIsPressed;
        }

        private Vector3 GetResolvedAimDirection()
        {
            if (_aimDirectionWorld.sqrMagnitude > MinInputSqrMagnitude)
            {
                return _aimDirectionWorld.normalized;
            }

            if (_lastNonZeroAimDirectionWorld.sqrMagnitude > MinInputSqrMagnitude)
            {
                return _lastNonZeroAimDirectionWorld.normalized;
            }

            return Vector3.forward;
        }

        private bool IsManualAimSpecified(
            ActionInputSource pressSource,
            Vector3 pressAimOffsetWorldFromPlayer)
        {
            switch (pressSource)
            {
                case ActionInputSource.Mouse:
                case ActionInputSource.Gamepad:
                    return !IsSameAimOffset(pressAimOffsetWorldFromPlayer, _aimOffsetWorldFromPlayer);

                default:
                    return false;
            }
        }

        private bool IsSameAimOffset(Vector3 pressOffset, Vector3 releaseOffset)
        {
            Vector3 delta = releaseOffset - pressOffset;
            delta.y = 0f;

            float threshold = Mathf.Max(0f, _sameAimOffsetDistanceThreshold);
            return delta.sqrMagnitude <= threshold * threshold;
        }

        private ActionInputSource GetPressSourceThisFrame(InputAction action)
        {
            if (action == null || !action.WasPressedThisFrame())
            {
                return ActionInputSource.None;
            }

            if (action.activeControl == null || action.activeControl.device == null)
            {
                return ActionInputSource.None;
            }

            if (action.activeControl.device is Mouse)
            {
                return ActionInputSource.Mouse;
            }

            if (action.activeControl.device is Gamepad)
            {
                return ActionInputSource.Gamepad;
            }

            return ActionInputSource.None;
        }

        private AimInputSource ConvertToAimInputSource(ActionInputSource actionInputSource)
        {
            switch (actionInputSource)
            {
                case ActionInputSource.Mouse:
                    return AimInputSource.Mouse;

                case ActionInputSource.Gamepad:
                    return AimInputSource.Gamepad;

                default:
                    return AimInputSource.None;
            }
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

        public bool TryConsumeAttackRelease(out ReleaseInfo info)
        {
            if (_hasPendingAttackRelease)
            {
                info = _pendingAttackRelease;
                _hasPendingAttackRelease = false;
                return true;
            }

            info = default;
            return false;
        }

        public bool TryConsumeSuperRelease(out ReleaseInfo info)
        {
            if (_hasPendingSuperRelease)
            {
                info = _pendingSuperRelease;
                _hasPendingSuperRelease = false;
                return true;
            }

            info = default;
            return false;
        }
    }
}
