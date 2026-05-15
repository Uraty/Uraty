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

        [Header("Combat")]
        [SerializeField]
        private float _attackRange = 3f;

        [SerializeField]
        private float _attackInterval = 1.2f;

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
        }

        /// <summary>
        /// Application 側から「死んだ/生きている」を更新する。
        /// </summary>
        public void SetIsDead(bool isDead)
        {
            _isDead = isDead;
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

            if (_findNearestEnemy == null)
            {
                ClearInputs();
                return;
            }

            _currentTarget = _findNearestEnemy.Invoke(_selfTransform, _searchRadius);

            if (_currentTarget == null)
            {
                ClearInputs();
                return;
            }

            Vector3 diff =
                _currentTarget.transform.position
                - _selfTransform.position;

            diff.y = 0f;

            float sqrDistance =
                diff.sqrMagnitude;

            if (sqrDistance <= MinDirectionSqrMagnitude)
            {
                ClearInputs();
                return;
            }

            float distance =
                Mathf.Sqrt(sqrDistance);

            Vector3 direction =
                diff.normalized;

            _aimDirectionWorld =
                direction;

            _aimPointWorld =
                _currentTarget.transform.position;

            if (distance > _attackRange)
            {
                _moveDirectionWorld =
                    direction;

                // 射程外に出たら再度インターバル計測し直す
                _attackTimer = 0f;
            }
            else
            {
                _moveDirectionWorld =
                    Vector3.zero;

                _attackTimer +=
                    Time.deltaTime;

                if (_attackTimer
                    >= _attackInterval)
                {
                    _attackTimer = 0f;
                    _attackReleasedThisFrame = true;
                }
            }
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
