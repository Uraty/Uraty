using UnityEngine;

namespace Uraty.Features.Bot
{
    [DefaultExecutionOrder(-100)]
    public sealed class BotInputInterpreter : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("Search")]
        [SerializeField]
        private float _searchRadius = 15f;

        [Header("Explore (No Target)")]
        [Tooltip("敵を見つけていない間の移動速度を抑える係数")]
        [SerializeField, Range(0f, 1f)]
        private float _exploreMoveScale = 0.75f;

        [Tooltip("探索方向を更新する間隔(秒)")]
        [SerializeField, Min(0.05f)]
        private float _exploreDirectionUpdateIntervalSeconds = 1.25f;

        [Tooltip("この距離以上動けていない場合にスタックと見なして再抽選する")]
        [SerializeField, Min(0f)]
        private float _exploreStuckDistanceMeters = 0.35f;

        [Tooltip("スタック判定のチェック間隔(秒)")]
        [SerializeField, Min(0.05f)]
        private float _exploreStuckCheckIntervalSeconds = 0.5f;

        [Header("Combat")]
        [SerializeField]
        private float _attackRange = 3f;

        [SerializeField]
        private float _attackInterval = 1.2f;

        [Header("Combat Movement")]
        [Tooltip("射程内での横移動（ストレイフ）の強さ(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _strafeMoveScale = 0.75f;

        [Tooltip("追い/引き（敵との距離調整）の強さ(0-1)")]
        [SerializeField, Range(0f, 1f)]
        private float _distanceAdjustMoveScale = 0.35f;

        [Tooltip("ストレイフ方向を反転する間隔(秒)")]
        [SerializeField, Min(0.05f)]
        private float _strafeSwitchIntervalSeconds = 1.0f;

        private Transform _selfTransform;
        private bool _isDead;

        private GameObject _currentTarget;

        /// <summary>
        /// BattleApplication 側が管理している「見えている敵」を返す。
        /// </summary>
        private System.Func<Transform, float, GameObject> _findNearestEnemy;

        private Vector3 _moveDirectionWorld;
        private Vector3 _aimDirectionWorld;
        private Vector3 _aimPointWorld;

        private bool _attackReleasedThisFrame;

        private float _attackTimer;

        // Explore state
        private Vector3 _exploreMoveDirectionWorld;
        private float _exploreDirectionTimer;

        private Vector3 _lastExploreCheckPosition;
        private float _exploreStuckCheckTimer;

        // Recovery state (provided by Application)
        private bool _isRecoveryMode;
        private Vector3 _recoveryMoveDirectionWorld;
        private float _recoveryMoveScale;

        // Combat movement state
        private float _strafeTimer;
        private float _strafeSign = 1f;

        public Vector3 MoveDirectionWorld =>
            _moveDirectionWorld;

        public Vector3 AimDirectionWorld =>
            _aimDirectionWorld;

        public Vector3 AimPointWorld =>
            _aimPointWorld;

        public bool AttackReleasedThisFrame =>
            _attackReleasedThisFrame;

        /// <summary>
        /// Bot が操作するキャラクター情報を Applicationから注入する。
        /// Character 側アセンブリへの参照を避けるため、必要最小限の情報だけを受け取る。
        /// </summary>
        public void Initialize(
            Transform selfTransform,
            System.Func<Transform, float, GameObject> findNearestEnemy)
        {
            _selfTransform = selfTransform;
            _findNearestEnemy = findNearestEnemy;

            _aimPointWorld = selfTransform != null
                ? selfTransform.position
                : Vector3.zero;

            ResetExploreState();
            ResetRecoveryState();
            ResetCombatMovementState();
        }

        /// <summary>
        /// Application 側から「死んだ/生きている」を更新する。
        /// </summary>
        public void SetIsDead(bool isDead)
        {
            _isDead = isDead;
        }

        /// <summary>
        /// Application 側から「回復待ち（逃走）モード」を更新する。
        /// Bot 側に CharacterStatus を持ち込まないため、状態は Application が判断して注入する。
        /// </summary>
        public void SetRecoveryMode(
            bool isRecoveryMode,
            Vector3 moveDirectionWorld,
            float moveScale)
        {
            _isRecoveryMode = isRecoveryMode;

            moveDirectionWorld.y = 0f;
            _recoveryMoveDirectionWorld = moveDirectionWorld;
            _recoveryMoveScale = Mathf.Clamp01(moveScale);
        }

        private void Update()
        {
            Think();
        }

        private void Think()
        {
            _attackReleasedThisFrame = false;

            if (_selfTransform == null)
            {
                return;
            }

            if (_isDead)
            {
                ClearInputs();
                return;
            }

            if (_isRecoveryMode)
            {
                ThinkRecovery();
                return;
            }

            if (_findNearestEnemy == null)
            {
                ClearInputs();
                return;
            }

            _currentTarget = _findNearestEnemy.Invoke(_selfTransform, _searchRadius);

            if (_currentTarget == null)
            {
                ThinkExplore();
                return;
            }

            ThinkCombatWithMovement(_currentTarget);
        }

        private void ThinkCombatWithMovement(GameObject target)
        {
            if (target == null)
            {
                ClearInputs();
                return;
            }

            Vector3 diff =
                target.transform.position
                - _selfTransform.position;

            diff.y = 0f;

            float sqrDistance =
                diff.sqrMagnitude;

            if (sqrDistance <= MinDirectionSqrMagnitude)
            {
                ClearInputs();
                return;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            Vector3 toEnemy = diff.normalized;

            _aimDirectionWorld = toEnemy;
            _aimPointWorld = target.transform.position;

            // ストレイフ方向を一定間隔で反転
            _strafeTimer += Time.deltaTime;
            if (_strafeTimer >= _strafeSwitchIntervalSeconds)
            {
                _strafeTimer = 0f;
                _strafeSign *= -1f;
            }

            Vector3 right = Vector3.Cross(Vector3.up, toEnemy);
            right.y = 0f;

            if (right.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                right = Vector3.right;
            }

            right.Normalize();

            // 距離調整（射程外は近づく / 射程内は少し離れる）
            float adjustSign = distance > _attackRange ? 1f : -0.25f;
            Vector3 adjust = toEnemy * (adjustSign * Mathf.Clamp01(_distanceAdjustMoveScale));
            Vector3 strafe = right * (_strafeSign * Mathf.Clamp01(_strafeMoveScale));

            Vector3 move = adjust + strafe;
            move.y = 0f;

            if (move.sqrMagnitude > 1.0f)
            {
                move.Normalize();
            }

            _moveDirectionWorld = move;

            // 攻撃タイマー（移動しながらでも撃つ）
            _attackTimer += Time.deltaTime;
            if (_attackTimer >= _attackInterval)
            {
                _attackTimer = 0f;
                _attackReleasedThisFrame = true;
            }
        }

        private void ThinkRecovery()
        {
            // 回復中は攻撃しない
            _attackTimer = 0f;

            Vector3 move = _recoveryMoveDirectionWorld;
            move.y = 0f;

            if (move.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                // 入力が来ていない場合は、とりあえず前方へ
                move = _selfTransform.forward;
                move.y = 0f;
            }

            if (move.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                move = Vector3.forward;
            }

            move.Normalize();

            _moveDirectionWorld = move * Mathf.Clamp01(_recoveryMoveScale);
            _aimDirectionWorld = move;
            _aimPointWorld = _selfTransform.position + move;
        }

        private void ThinkExplore()
        {
            // 探索中は攻撃関連をリセット
            _attackTimer = 0f;

            UpdateExploreDirectionIfNeeded();
            UpdateExploreStuckIfNeeded();

            Vector3 move = _exploreMoveDirectionWorld;
            move.y = 0f;

            if (move.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                SetNewExploreDirection();
                move = _exploreMoveDirectionWorld;
            }

            //目的地が無いので、向いている方向へ狙いを向けておく
            _aimDirectionWorld = move.sqrMagnitude > MinDirectionSqrMagnitude
                ? move.normalized
                : Vector3.zero;

            _aimPointWorld = _selfTransform.position + _aimDirectionWorld;

            _moveDirectionWorld = move.normalized * Mathf.Clamp01(_exploreMoveScale);
        }

        private void UpdateExploreDirectionIfNeeded()
        {
            _exploreDirectionTimer += Time.deltaTime;

            if (_exploreDirectionTimer < _exploreDirectionUpdateIntervalSeconds)
            {
                return;
            }

            _exploreDirectionTimer = 0f;
            SetNewExploreDirection();
        }

        private void UpdateExploreStuckIfNeeded()
        {
            _exploreStuckCheckTimer += Time.deltaTime;

            if (_exploreStuckCheckTimer < _exploreStuckCheckIntervalSeconds)
            {
                return;
            }

            _exploreStuckCheckTimer = 0f;

            Vector3 current = _selfTransform.position;
            Vector3 delta = current - _lastExploreCheckPosition;
            delta.y = 0f;

            if (delta.magnitude < _exploreStuckDistanceMeters)
            {
                // スタック気味なので方向を変える
                SetNewExploreDirection();
            }

            _lastExploreCheckPosition = current;
        }

        private void SetNewExploreDirection()
        {
            if (_selfTransform == null)
            {
                _exploreMoveDirectionWorld = Vector3.zero;
                return;
            }

            // 前方±90度の範囲でランダムに散策
            float angle = Random.Range(-90f, 90f);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * _selfTransform.forward;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = Vector3.forward;
            }

            _exploreMoveDirectionWorld = direction.normalized;
        }

        private void ResetExploreState()
        {
            _exploreMoveDirectionWorld = Vector3.zero;
            _exploreDirectionTimer = 0f;
            _exploreStuckCheckTimer = 0f;

            if (_selfTransform != null)
            {
                _lastExploreCheckPosition = _selfTransform.position;
            }
        }

        private void ResetRecoveryState()
        {
            _isRecoveryMode = false;
            _recoveryMoveDirectionWorld = Vector3.zero;
            _recoveryMoveScale = 0f;
        }

        private void ResetCombatMovementState()
        {
            _strafeTimer = 0f;
            _strafeSign = Random.value < 0.5f ? -1f : 1f;
        }

        private void ClearInputs()
        {
            _moveDirectionWorld =
                Vector3.zero;

            _aimDirectionWorld =
                Vector3.zero;

            if (_selfTransform != null)
            {
                _aimPointWorld =
                    _selfTransform.position;
            }
        }
    }
}
