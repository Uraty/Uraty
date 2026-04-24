using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Player
{
    /// <summary>
    /// InputActions の生の入力を、
    /// ゲーム側で使いやすい形へ変換して保持するクラス。
    ///
    /// このクラスの主な責務
    /// ・Move をカメラ基準のワールド方向へ変換する
    /// ・Aim の入力元が Mouse / Gamepad のどちらかを整理する
    /// ・Mouse / Gamepad 共通で「プレイヤー基準の照準オフセット」を更新する
    /// ・その照準オフセットから、画面座標とワールド方向を作る
    /// ・Attack / Super の押下状態を外へ公開する
    /// ・Attack / Super の Release 時に、オートエイム可否を判断する
    ///
    /// このクラスは「攻撃を実行する」「予測メッシュを出す」まではやらない。
    /// そこは PlayerAim / PlayerCombat 側の責務。
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
            /// この Release をオートエイム扱いにしてよいか。
            /// </summary>
            public bool CanAutoAim;

            /// <summary>
            /// Release 時点で解決された Aim 方向。
            /// Aim がゼロなら、最後の有効方向を使う。
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
        [Tooltip("Mouse Delta に掛けるゲーム内感度")]
        [Min(0f)]
        [SerializeField] private float _mouseAimSensitivity = 1.0f;

        [Tooltip("このピクセル以下の Mouse Delta は無視する")]
        [Min(0f)]
        [SerializeField] private float _mouseAimDeltaDeadZonePixels = 0.5f;

        [Header("Gamepad Aim")]
        [Tooltip("右スティックで照準オフセットを動かす速さ（px/sec）")]
        [Min(0f)]
        [SerializeField] private float _gamepadAimMoveSpeedPixelsPerSecond = 900f;

        [Tooltip("右スティック移動用デッドゾーン")]
        [Range(0f, 1f)]
        [SerializeField] private float _gamepadAimMoveDeadZone = 0.20f;

        [Tooltip("オートエイム判定用の右スティックしきい値")]
        [Range(0f, 1f)]
        [SerializeField] private float _gamepadAutoAimDeadZone = 0.25f;

        [Header("Shared Aim Offset")]
        [Tooltip("プレイヤー基準の照準オフセット最大長（px）")]
        [Min(0f)]
        [SerializeField] private float _maxAimOffsetPixels = 600f;

        [Header("Auto Aim")]
        [Tooltip("押した時と離した時のオフセット差がこのピクセル以内なら『同じ位置』とみなす")]
        [Min(0f)]
        [SerializeField] private float _sameAimOffsetPixelThreshold = 2f;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLog = false;

        /// <summary>
        /// InputActionReference から実体の InputAction を取り出すためのショートカット。
        /// </summary>
        private InputAction MoveAction => _moveActionReference != null ? _moveActionReference.action : null;
        private InputAction AimAction => _aimActionReference != null ? _aimActionReference.action : null;
        private InputAction AttackAction => _attackActionReference != null ? _attackActionReference.action : null;
        private InputAction SuperAction => _superActionReference != null ? _superActionReference.action : null;

        /// <summary>
        /// 生の Move / Aim 入力値。
        /// Move は Vector2、Aim も Vector2。
        ///
        /// 前提:
        /// ・Mouse Aim は Delta
        /// ・Gamepad Aim は Right Stick
        /// </summary>
        private Vector2 _rawMoveInput;
        private Vector2 _rawAimInput;

        /// <summary>
        /// プレイヤー画面位置からの照準オフセット。
        /// Mouse / Gamepad 共通でこれを更新する。
        ///
        /// Aim 開始時は 0 に戻すので、
        /// 「照準開始位置はプレイヤー位置」になる。
        /// </summary>
        private Vector2 _aimScreenOffsetFromPlayer;

        /// <summary>
        /// ゲーム側が使う変換後の値。
        /// </summary>
        private Vector3 _moveDirectionWorld;
        private Vector3 _aimDirectionWorld;
        private Vector3 _lastNonZeroAimDirectionWorld = Vector3.forward;

        /// <summary>
        /// 現在の Aim 入力元。
        /// </summary>
        private AimInputSource _currentAimSource = AimInputSource.None;

        /// <summary>
        /// Attack / Super の Release を検出するための前フレーム押下状態。
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
        /// Aim 開始時の入力元。
        /// Release 時の「手動指定あり / なし」判定に使う。
        /// </summary>
        private ActionInputSource _attackPressSource = ActionInputSource.None;
        private ActionInputSource _superPressSource = ActionInputSource.None;

        /// <summary>
        /// Attack / Super を押した瞬間の照準オフセット。
        /// 押した時と離した時で差があるかを見る。
        /// </summary>
        private Vector2 _attackPressAimOffsetFromPlayer;
        private Vector2 _superPressAimOffsetFromPlayer;

        // ===== 外部公開プロパティ =====

        public Vector2 RawMoveInput => _rawMoveInput;
        public Vector2 RawAimInput => _rawAimInput;

        public Vector3 MoveDirectionWorld => _moveDirectionWorld;
        public Vector3 AimDirectionWorld => _aimDirectionWorld;
        public Vector3 LastNonZeroAimDirectionWorld => _lastNonZeroAimDirectionWorld;

        public AimInputSource CurrentAimSource => _currentAimSource;
        public bool IsAimFromGamepad => _currentAimSource == AimInputSource.Gamepad;
        public bool IsAimFromMouse => _currentAimSource == AimInputSource.Mouse;

        /// <summary>
        /// 現在の照準画面座標。
        /// PlayerAimFromInterpreter 側はこれを Ray に変換して使う。
        /// </summary>
        public Vector2 CurrentAimScreenPosition => GetCurrentAimScreenPosition();

        /// <summary>
        /// 既存コードとの互換用。
        /// 中身は CurrentAimScreenPosition と同じ。
        /// </summary>
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
            // Range 属性だけだと、スクリプト経由や古いシリアライズ値まで完全には守れない。
            // なので内部でも補正しておく。
            _gamepadAimMoveDeadZone = Mathf.Clamp01(_gamepadAimMoveDeadZone);
            _gamepadAutoAimDeadZone = Mathf.Clamp01(_gamepadAutoAimDeadZone);

            _mouseAimSensitivity = Mathf.Max(0f, _mouseAimSensitivity);
            _mouseAimDeltaDeadZonePixels = Mathf.Max(0f, _mouseAimDeltaDeadZonePixels);
            _gamepadAimMoveSpeedPixelsPerSecond = Mathf.Max(0f, _gamepadAimMoveSpeedPixelsPerSecond);
            _maxAimOffsetPixels = Mathf.Max(0f, _maxAimOffsetPixels);
            _sameAimOffsetPixelThreshold = Mathf.Max(0f, _sameAimOffsetPixelThreshold);
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

            // 照準開始前の初期値。
            // 実際には Aim 開始時に 0 へ戻すので、ここは安全な初期値でよい。
            _aimScreenOffsetFromPlayer = Vector2.zero;

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
                    $"offset={_aimScreenOffsetFromPlayer}, " +
                    $"screen={CurrentAimScreenPosition}, " +
                    $"aimDir={_aimDirectionWorld}");
            }
        }

        /// <summary>
        /// InputAction から生の入力値を読む。
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
        /// 足りない場合は Attack / Super を押したデバイスから補完する。
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
        /// つまり Aim 開始位置は常にプレイヤー位置になる。
        /// </summary>
        private void CachePressContext()
        {
            if (AttackPressedThisFrame)
            {
                _attackPressSource = AttackPressedSourceThisFrame;

                if (_attackPressSource == ActionInputSource.Mouse ||
                    _attackPressSource == ActionInputSource.Gamepad)
                {
                    _aimScreenOffsetFromPlayer = Vector2.zero;
                    _attackPressAimOffsetFromPlayer = _aimScreenOffsetFromPlayer;
                }
            }

            if (SuperPressedThisFrame)
            {
                _superPressSource = SuperPressedSourceThisFrame;

                if (_superPressSource == ActionInputSource.Mouse ||
                    _superPressSource == ActionInputSource.Gamepad)
                {
                    _aimScreenOffsetFromPlayer = Vector2.zero;
                    _superPressAimOffsetFromPlayer = _aimScreenOffsetFromPlayer;
                }
            }
        }

        /// <summary>
        /// Aim 中の入力に応じて照準オフセットを更新する。
        ///
        /// Mouse:
        /// ・delta に感度を掛ける
        /// ・微小移動はデッドゾーンで無視
        ///
        /// Gamepad:
        /// ・右スティックに半径デッドゾーンをかける
        /// ・速度 × deltaTime でオフセットを動かす
        ///
        /// 最後にオフセットの最大長を制限する。
        /// </summary>
        private void UpdateAimOffsetFromCurrentInput()
        {
            if (!IsAnyAimButtonPressed())
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

                        _aimScreenOffsetFromPlayer += mouseDelta * _mouseAimSensitivity;
                        break;
                    }

                case AimInputSource.Gamepad:
                    {
                        Vector2 processedStick = ApplyRadialDeadZone(_rawAimInput, _gamepadAimMoveDeadZone);
                        _aimScreenOffsetFromPlayer += processedStick * _gamepadAimMoveSpeedPixelsPerSecond * Time.deltaTime;
                        break;
                    }

                case AimInputSource.None:
                default:
                    break;
            }

            float maxLength = Mathf.Max(0f, _maxAimOffsetPixels);
            if (maxLength > 0f && _aimScreenOffsetFromPlayer.magnitude > maxLength)
            {
                _aimScreenOffsetFromPlayer = _aimScreenOffsetFromPlayer.normalized * maxLength;
            }
        }

        /// <summary>
        /// 生の Move / Aim をゲーム側で使う値へ変換する。
        /// </summary>
        private void RefreshConvertedValues()
        {
            _moveDirectionWorld = ConvertInputToCameraRelativeDirection(_rawMoveInput);

            if (_currentAimSource == AimInputSource.Mouse || _currentAimSource == AimInputSource.Gamepad)
            {
                Vector2 aimScreenPosition = GetCurrentAimScreenPosition();
                _aimDirectionWorld = ConvertScreenPointToWorldDirection(aimScreenPosition);
            }
            else
            {
                _aimDirectionWorld = Vector3.zero;
            }

            if (_aimDirectionWorld.sqrMagnitude > MinInputSqrMagnitude)
            {
                _lastNonZeroAimDirectionWorld = _aimDirectionWorld.normalized;
            }
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
            if (_targetCamera == null)
            {
                return Vector3.zero;
            }

            if (input.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return Vector3.zero;
            }

            Vector3 cameraForward = _targetCamera.transform.forward;
            Vector3 cameraRight = _targetCamera.transform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            if (cameraForward.sqrMagnitude <= MinInputSqrMagnitude ||
                cameraRight.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return Vector3.zero;
            }

            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 direction = (cameraRight * input.x) + (cameraForward * input.y);
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return Vector3.zero;
            }

            return direction.normalized;
        }

        /// <summary>
        /// 現在の照準画面座標を、プレイヤー中心からのワールド方向へ変換する。
        /// Mouse / Gamepad 共通。
        /// </summary>
        private Vector3 ConvertScreenPointToWorldDirection(Vector2 screenPoint)
        {
            if (_targetCamera == null || _playerCenter == null)
            {
                return Vector3.zero;
            }

            Ray ray = _targetCamera.ScreenPointToRay(screenPoint);
            Plane plane = new Plane(Vector3.up, _playerCenter.position);

            if (!plane.Raycast(ray, out float enter))
            {
                return Vector3.zero;
            }

            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 direction = hitPoint - _playerCenter.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinInputSqrMagnitude)
            {
                return Vector3.zero;
            }

            return direction.normalized;
        }

        /// <summary>
        /// 現在の照準画面座標。
        /// プレイヤーの今の画面位置にオフセットを足して作る。
        ///
        /// なので、オフセットが 0 なら
        /// プレイヤーが動いても照準はプレイヤーに追従する。
        /// </summary>
        private Vector2 GetCurrentAimScreenPosition()
        {
            Vector2 playerScreenPosition = GetPlayerScreenPosition();
            Vector2 result = playerScreenPosition + _aimScreenOffsetFromPlayer;

            result.x = Mathf.Clamp(result.x, 0f, Screen.width);
            result.y = Mathf.Clamp(result.y, 0f, Screen.height);

            return result;
        }

        /// <summary>
        /// プレイヤー中心の現在画面座標を返す。
        /// </summary>
        private Vector2 GetPlayerScreenPosition()
        {
            if (_targetCamera == null || _playerCenter == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 screen = _targetCamera.WorldToScreenPoint(_playerCenter.position);
            return new Vector2(screen.x, screen.y);
        }

        /// <summary>
        /// Aim ボタンを押している間だけ、照準オフセットを更新したいので使う。
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

        /// <summary>
        /// Attack Release 時のオートエイム可否。
        /// </summary>
        private bool EvaluateAutoAimForAttack()
        {
            return !IsManualAimSpecifiedForAttack();
        }

        /// <summary>
        /// Super Release 時のオートエイム可否。
        /// </summary>
        private bool EvaluateAutoAimForSuper()
        {
            return !IsManualAimSpecifiedForSuper();
        }

        /// <summary>
        /// 押した時のオフセットと離した時のオフセットが違えば、
        /// 「手動で方向指定した」とみなす。
        /// Mouse / Gamepad 共通。
        /// </summary>
        private bool IsManualAimSpecifiedForAttack()
        {
            switch (_attackPressSource)
            {
                case ActionInputSource.Mouse:
                case ActionInputSource.Gamepad:
                    return !IsSameAimOffset(_attackPressAimOffsetFromPlayer, _aimScreenOffsetFromPlayer);

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
                    return !IsSameAimOffset(_superPressAimOffsetFromPlayer, _aimScreenOffsetFromPlayer);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Aim 開始時と終了時のオフセット差が小さければ、
        /// 「動かしていない」とみなす。
        /// </summary>
        private bool IsSameAimOffset(Vector2 pressOffset, Vector2 releaseOffset)
        {
            float threshold = Mathf.Max(0f, _sameAimOffsetPixelThreshold);
            float sqrThreshold = threshold * threshold;

            return (releaseOffset - pressOffset).sqrMagnitude <= sqrThreshold;
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

        // ===== 外から Release 情報を回収する入口 =====

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
