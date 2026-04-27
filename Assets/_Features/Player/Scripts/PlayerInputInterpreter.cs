using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Player
{
    /// <summary>
    /// InputActions の生入力を、ゲーム側で使いやすい形に変換して保持するクラス。
    ///
    /// この版では、Aim を「画面座標」ではなく
    /// 「プレイヤー基準の地面上オフセット」で管理する。
    ///
    /// これにより、
    /// ・カメラ角度で着弾点の届く範囲が変わらない
    /// ・画面外にも着弾点を伸ばせる
    /// ・Mouse / Gamepad を同じ考え方で扱える
    /// ようになる。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputInterpreter : MonoBehaviour
    {
        /// <summary>
        /// 現在の Aim 入力元。
        /// </summary>
        public enum AimInputSource
        {
            None,
            Gamepad,
            Mouse,
        }

        /// <summary>
        /// Attack / Super を押した瞬間の入力元。
        /// Release 時の判定で使う。
        /// </summary>
        public enum ActionInputSource
        {
            None,
            Gamepad,
            Mouse,
        }

        /// <summary>
        /// Attack / Super の Release 時に外へ渡す情報。
        /// </summary>
        public struct ReleaseInfo
        {
            /// <summary>
            /// オートエイムしてよいか。
            /// </summary>
            public bool CanAutoAim;

            /// <summary>
            /// Release 時点での Aim 方向。
            /// 現フレームで方向が取れない場合は、最後の有効方向を使う。
            /// </summary>
            public Vector3 AimDirection;

            public ReleaseInfo(bool canAutoAim, Vector3 aimDirection)
            {
                CanAutoAim = canAutoAim;
                AimDirection = aimDirection;
            }
        }

        /// <summary>
        /// ほぼゼロ入力とみなす最小値。
        /// sqrMagnitude と比較する前提。
        /// </summary>
        private const float MinInputSqrMagnitude = 0.0001f;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference _moveActionReference;
        [SerializeField] private InputActionReference _aimActionReference;
        [SerializeField] private InputActionReference _attackActionReference;
        [SerializeField] private InputActionReference _superActionReference;

        [Header("References")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private Transform _playerCenter;

        [Header("Mouse Aim")]
        [Tooltip("Mouse Delta に掛ける感度。単位は『地面上距離 / 1px』")]
        [Min(0f)]
        [SerializeField] private float _mouseAimSensitivity = 0.02f;

        [Tooltip("このピクセル以下の Mouse Delta は無視する")]
        [Min(0f)]
        [SerializeField] private float _mouseAimDeltaDeadZonePixels = 0.5f;

        [Header("Gamepad Aim")]
        [Tooltip("右スティックで地面上オフセットを動かす速さ（距離/秒）")]
        [Min(0f)]
        [SerializeField] private float _gamepadAimMoveSpeedUnitsPerSecond = 10f;

        [Tooltip("右スティック移動用デッドゾーン")]
        [Range(0f, 1f)]
        [SerializeField] private float _gamepadAimMoveDeadZone = 0.20f;

        [Tooltip("オートエイム判定用の右スティックしきい値")]
        [Range(0f, 1f)]
        [SerializeField] private float _gamepadAutoAimDeadZone = 0.25f;

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

        /// <summary>
        /// InputActionReference から実体の InputAction を取り出すショートカット。
        /// </summary>
        private InputAction MoveAction => _moveActionReference != null ? _moveActionReference.action : null;
        private InputAction AimAction => _aimActionReference != null ? _aimActionReference.action : null;
        private InputAction AttackAction => _attackActionReference != null ? _attackActionReference.action : null;
        private InputAction SuperAction => _superActionReference != null ? _superActionReference.action : null;

        /// <summary>
        /// 生の Move / Aim 入力値。
        /// Aim は
        /// ・Mouse: Delta
        /// ・Gamepad: Right Stick
        /// を読む前提。
        /// </summary>
        private Vector2 _rawMoveInput;
        private Vector2 _rawAimInput;

        /// <summary>
        /// プレイヤー位置からの地面上オフセット。
        /// Y は常に使わないので 0 として扱う。
        ///
        /// Mouse / Gamepad 共通で、この値を更新する。
        /// </summary>
        private Vector3 _aimOffsetWorldFromPlayer = Vector3.zero;

        /// <summary>
        /// 変換後の出力。
        /// </summary>
        private Vector3 _moveDirectionWorld;
        private Vector3 _aimDirectionWorld;
        private Vector3 _lastNonZeroAimDirectionWorld = Vector3.forward;

        /// <summary>
        /// 現在の Aim 点が有効か。
        /// オフセットがほぼゼロなら false になる。
        /// </summary>
        private bool _hasValidAimPointWorld;
        private Vector3 _aimPointWorld;

        /// <summary>
        /// 現在の Aim 入力元。
        /// </summary>
        private AimInputSource _currentAimSource = AimInputSource.None;

        /// <summary>
        /// Release 検出用の前フレーム押下状態。
        /// </summary>
        private bool _previousAttackPressed;
        private bool _previousSuperPressed;

        /// <summary>
        /// Release 情報を 1 回だけ回収できるように保持する。
        /// </summary>
        private bool _hasPendingAttackRelease;
        private bool _hasPendingSuperRelease;

        private ReleaseInfo _pendingAttackRelease;
        private ReleaseInfo _pendingSuperRelease;

        /// <summary>
        /// Attack / Super を押した瞬間の入力元。
        /// Release 時の判定に使う。
        /// </summary>
        private ActionInputSource _attackPressSource = ActionInputSource.None;
        private ActionInputSource _superPressSource = ActionInputSource.None;

        /// <summary>
        /// Attack / Super を押した瞬間のオフセット。
        /// 「押した時からどれだけ動かしたか」を見る。
        /// </summary>
        private Vector3 _attackPressAimOffsetWorldFromPlayer = Vector3.zero;
        private Vector3 _superPressAimOffsetWorldFromPlayer = Vector3.zero;

        // ===== 外部公開 =====

        public Vector2 RawMoveInput => _rawMoveInput;
        public Vector2 RawAimInput => _rawAimInput;

        public Vector3 MoveDirectionWorld => _moveDirectionWorld;
        public Vector3 AimDirectionWorld => _aimDirectionWorld;
        public Vector3 LastNonZeroAimDirectionWorld => _lastNonZeroAimDirectionWorld;

        public bool HasValidAimPointWorld => _hasValidAimPointWorld;
        public Vector3 AimPointWorld => _aimPointWorld;

        public AimInputSource CurrentAimSource => _currentAimSource;
        public bool IsAimFromGamepad => _currentAimSource == AimInputSource.Gamepad;
        public bool IsAimFromMouse => _currentAimSource == AimInputSource.Mouse;

        /// <summary>
        /// 互換用。
        /// 画面上の照準位置が必要な UI がある時のために残している。
        /// 中身は AimPointWorld を画面へ投影した座標。
        /// </summary>
        public Vector2 CurrentAimScreenPosition => GetCurrentAimScreenPosition();
        public Vector2 CurrentMouseScreenPosition => GetCurrentAimScreenPosition();

        // 押下状態
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
            // Inspector でも内部でも範囲が崩れないように補正する。
            _gamepadAimMoveDeadZone = Mathf.Clamp01(_gamepadAimMoveDeadZone);
            _gamepadAutoAimDeadZone = Mathf.Clamp01(_gamepadAutoAimDeadZone);

            _mouseAimSensitivity = Mathf.Max(0f, _mouseAimSensitivity);
            _mouseAimDeltaDeadZonePixels = Mathf.Max(0f, _mouseAimDeltaDeadZonePixels);
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

            if (_playerCenter == null)
            {
                _playerCenter = transform;
            }

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
            MoveAction?.Enable();
            AimAction?.Enable();
            AttackAction?.Enable();
            SuperAction?.Enable();

            _previousAttackPressed = AttackIsPressed;
            _previousSuperPressed = SuperIsPressed;

            RefreshInputs();
            RefreshAimSource();
            CachePressContext();
            UpdateAimOffsetFromCurrentInput();
            RefreshConvertedValues();
        }

        private void OnDisable()
        {
            MoveAction?.Disable();
            AimAction?.Disable();
            AttackAction?.Disable();
            SuperAction?.Disable();
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
                    $"offsetWorld={_aimOffsetWorldFromPlayer}, " +
                    $"aimPointWorld={_aimPointWorld}, " +
                    $"aimDir={_aimDirectionWorld}");
            }
        }

        /// <summary>
        /// InputAction から生入力を読む。
        /// </summary>
        private void RefreshInputs()
        {
            if (MoveAction != null)
            {
                _rawMoveInput = MoveAction.ReadValue<Vector2>();
            }
            else
            {
                _rawMoveInput = Vector2.zero;
            }

            if (AimAction != null)
            {
                _rawAimInput = AimAction.ReadValue<Vector2>();
            }
            else
            {
                _rawAimInput = Vector2.zero;
            }
        }

        /// <summary>
        /// Aim の入力元を決定する。
        /// まずは AimAction の activeControl を優先し、
        /// 足りない時は Attack / Super を押したデバイスから補完する。
        /// </summary>
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

        /// <summary>
        /// Aim 開始時の文脈を保存する。
        ///
        /// ここで Mouse / Gamepad 共通でオフセットを 0 に戻す。
        /// つまり Aim 開始地点は常にプレイヤー位置。
        /// </summary>
        private void CachePressContext()
        {
            if (AttackPressedThisFrame)
            {
                _attackPressSource = AttackPressedSourceThisFrame;

                if (_attackPressSource is ActionInputSource.Mouse or ActionInputSource.Gamepad)
                {
                    _aimOffsetWorldFromPlayer = Vector3.zero;
                    _attackPressAimOffsetWorldFromPlayer = _aimOffsetWorldFromPlayer;
                }
            }

            if (SuperPressedThisFrame)
            {
                _superPressSource = SuperPressedSourceThisFrame;

                if (_superPressSource is ActionInputSource.Mouse or ActionInputSource.Gamepad)
                {
                    _aimOffsetWorldFromPlayer = Vector3.zero;
                    _superPressAimOffsetWorldFromPlayer = _aimOffsetWorldFromPlayer;
                }
            }
        }

        /// <summary>
        /// 現在の入力に応じて、プレイヤー基準の地面上オフセットを更新する。
        ///
        /// Mouse:
        /// ・delta に感度を掛ける
        /// ・微小移動は無視する
        ///
        /// Gamepad:
        /// ・右スティックに半径デッドゾーンをかける
        /// ・速度 × deltaTime で動かす
        ///
        /// 最後にオフセットの最大長を制限する。
        /// </summary>
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
                    {
                        Vector2 mouseDelta = _rawAimInput;

                        float mouseDeadZoneSqr = _mouseAimDeltaDeadZonePixels * _mouseAimDeltaDeadZonePixels;
                        if (mouseDelta.sqrMagnitude <= mouseDeadZoneSqr)
                        {
                            mouseDelta = Vector2.zero;
                        }

                        // 画面上の左右移動 -> カメラ右方向
                        // 画面上の上下移動 -> カメラ前方向
                        Vector3 deltaWorld =
                            flatRight * (mouseDelta.x * _mouseAimSensitivity) +
                            flatForward * (mouseDelta.y * _mouseAimSensitivity);

                        _aimOffsetWorldFromPlayer += deltaWorld;
                        break;
                    }

                case AimInputSource.Gamepad:
                    {
                        Vector2 processedStick = ApplyRadialDeadZone(_rawAimInput, _gamepadAimMoveDeadZone);

                        Vector3 deltaWorld =
                            flatRight * (processedStick.x * _gamepadAimMoveSpeedUnitsPerSecond * Time.deltaTime) +
                            flatForward * (processedStick.y * _gamepadAimMoveSpeedUnitsPerSecond * Time.deltaTime);

                        _aimOffsetWorldFromPlayer += deltaWorld;
                        break;
                    }

                case AimInputSource.None:
                default:
                    break;
            }

            // Y は使わないので固定
            _aimOffsetWorldFromPlayer.y = 0f;

            float maxLength = Mathf.Max(0f, _maxAimOffsetDistance);
            if (maxLength > 0f)
            {
                Vector3 flatOffset = _aimOffsetWorldFromPlayer;
                flatOffset.y = 0f;

                if (flatOffset.magnitude > maxLength)
                {
                    flatOffset = flatOffset.normalized * maxLength;
                    _aimOffsetWorldFromPlayer = flatOffset;
                }
            }
        }

        /// <summary>
        /// 生入力から、ゲーム側で使う Move / Aim を作る。
        /// </summary>
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

                if (_playerCenter != null)
                {
                    _aimPointWorld = _playerCenter.position;
                }
                else
                {
                    _aimPointWorld = Vector3.zero;
                }

                _aimDirectionWorld = Vector3.zero;
            }
        }

        /// <summary>
        /// 現在の地面上オフセットから、
        /// Aim 点と Aim 方向を作る。
        /// </summary>
        private bool TryBuildAimPointAndDirection(out Vector3 aimPointWorld, out Vector3 aimDirectionWorld)
        {
            aimPointWorld = Vector3.zero;
            aimDirectionWorld = Vector3.zero;

            if (_playerCenter == null)
            {
                return false;
            }

            Vector3 offset = _aimOffsetWorldFromPlayer;
            offset.y = 0f;

            if (offset.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return false;
            }

            aimPointWorld = _playerCenter.position + offset;
            aimPointWorld.y = _playerCenter.position.y;

            aimDirectionWorld = offset.normalized;
            return true;
        }

        /// <summary>
        /// Attack / Super の Release を検出して、1回だけ回収できる情報を作る。
        /// </summary>
        private void DetectReleaseTriggers()
        {
            bool currentAttackPressed = AttackIsPressed;
            bool currentSuperPressed = SuperIsPressed;

            if (_previousAttackPressed && !currentAttackPressed)
            {
                bool canAutoAim = EvaluateAutoAimForAttack();
                Vector3 aimDirection = GetResolvedAimDirection();

                _pendingAttackRelease = new ReleaseInfo(canAutoAim, aimDirection);
                _hasPendingAttackRelease = true;

                _attackPressSource = ActionInputSource.None;
            }

            if (_previousSuperPressed && !currentSuperPressed)
            {
                bool canAutoAim = EvaluateAutoAimForSuper();
                Vector3 aimDirection = GetResolvedAimDirection();

                _pendingSuperRelease = new ReleaseInfo(canAutoAim, aimDirection);
                _hasPendingSuperRelease = true;

                _superPressSource = ActionInputSource.None;
            }

            _previousAttackPressed = currentAttackPressed;
            _previousSuperPressed = currentSuperPressed;
        }

        /// <summary>
        /// Move 用。
        /// 2D 入力をカメラ基準のワールド方向へ変換する。
        /// 長さは捨てて方向だけ返す。
        /// </summary>
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

        /// <summary>
        /// カメラの right / forward を地面上へ投影した基底を作る。
        /// </summary>
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

        /// <summary>
        /// 画面上の照準位置が必要な UI 用。
        /// AimPointWorld を画面へ投影する。
        /// Aim 無効時はプレイヤー位置を返す。
        /// </summary>
        private Vector2 GetCurrentAimScreenPosition()
        {
            if (_targetCamera == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 world = _hasValidAimPointWorld && _playerCenter != null
                ? _aimPointWorld
                : (_playerCenter != null ? _playerCenter.position : Vector3.zero);

            Vector3 screen = _targetCamera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        /// <summary>
        /// Aim ボタンを押している間だけ、オフセットを更新する。
        /// </summary>
        private bool IsAnyAimButtonPressed()
        {
            return AttackIsPressed || SuperIsPressed;
        }

        /// <summary>
        /// Release 時に使う方向。
        /// 今フレームの方向が無ければ最後の有効方向を使う。
        /// </summary>
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

        private bool EvaluateAutoAimForAttack()
        {
            return !IsManualAimSpecifiedForAttack();
        }

        private bool EvaluateAutoAimForSuper()
        {
            return !IsManualAimSpecifiedForSuper();
        }

        /// <summary>
        /// 押した時のオフセットと離した時のオフセットが違えば、
        /// 手動で方向指定したとみなす。
        /// Mouse / Gamepad 共通。
        /// </summary>
        private bool IsManualAimSpecifiedForAttack()
        {
            switch (_attackPressSource)
            {
                case ActionInputSource.Mouse:
                case ActionInputSource.Gamepad:
                    return !IsSameAimOffset(_attackPressAimOffsetWorldFromPlayer, _aimOffsetWorldFromPlayer);

                default:
                    return false;
            }
        }

        private bool IsManualAimSpecifiedForSuper()
        {
            switch (_superPressSource)
            {
                case ActionInputSource.Mouse:
                case ActionInputSource.Gamepad:
                    return !IsSameAimOffset(_superPressAimOffsetWorldFromPlayer, _aimOffsetWorldFromPlayer);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Aim 開始時と終了時のオフセット差が小さければ、
        /// 動かしていないとみなす。
        /// </summary>
        private bool IsSameAimOffset(Vector3 pressOffset, Vector3 releaseOffset)
        {
            Vector3 delta = releaseOffset - pressOffset;
            delta.y = 0f;

            float threshold = Mathf.Max(0f, _sameAimOffsetDistanceThreshold);
            return delta.sqrMagnitude <= threshold * threshold;
        }

        /// <summary>
        /// Attack / Super を押した瞬間のデバイスを取得する。
        /// </summary>
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

        /// <summary>
        /// スティック入力に半径デッドゾーンを適用する。
        ///
        /// 0〜deadZone:
        /// ・0扱い
        ///
        /// deadZone〜1:
        /// ・0〜1 に詰め直して返す
        /// </summary>
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

        // ===== Release 情報の回収 =====

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
