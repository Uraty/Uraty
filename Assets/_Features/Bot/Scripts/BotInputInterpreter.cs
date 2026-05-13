using UnityEngine;

using Uraty.Features.Character;

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

        private CharacterStatus _selfStatus;

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

        public void Initialize(
            CharacterStatus selfStatus)
        {
            _selfStatus = selfStatus;
        }

        private void Update()
        {
            Think();
        }

        private void Think()
        {
            _attackReleasedThisFrame = false;

            if (_selfStatus == null)
            {
                return;
            }

            if (_selfStatus.IsDead)
            {
                ClearInputs();
                return;
            }

            GameObject target = FindNearestEnemy();

            if (target == null)
            {
                ClearInputs();
                return;
            }

            Vector3 diff =
                target.transform.position
                - _selfStatus.transform.position;

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
                target.transform.position;

            if (distance > _attackRange)
            {
                _moveDirectionWorld =
                    direction;
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

        private GameObject FindNearestEnemy()
        {
            CharacterStatus[] allStatuses =
                FindObjectsByType<CharacterStatus>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            GameObject nearest = null;

            float nearestSqrDistance =
                float.MaxValue;

            foreach (CharacterStatus other in allStatuses)
            {
                if (other == null)
                {
                    continue;
                }

                if (other == _selfStatus)
                {
                    continue;
                }

                if (other.IsDead)
                {
                    continue;
                }

                if (other.TeamId
                    == _selfStatus.TeamId)
                {
                    continue;
                }

                if (other.IsInsideBush)
                {
                    continue;
                }

                Vector3 diff =
                    other.transform.position
                    - _selfStatus.transform.position;

                diff.y = 0f;

                float sqrDistance =
                    diff.sqrMagnitude;

                if (sqrDistance >
                    _searchRadius
                    * _searchRadius)
                {
                    continue;
                }

                if (sqrDistance <
                    nearestSqrDistance)
                {
                    nearestSqrDistance =
                        sqrDistance;

                    nearest =
                        other.gameObject;
                }
            }

            return nearest;
        }

        private void ClearInputs()
        {
            _moveDirectionWorld =
                Vector3.zero;

            _aimDirectionWorld =
                Vector3.zero;

            if (_selfStatus != null)
            {
                _aimPointWorld =
                    _selfStatus.transform.position;
            }
        }
    }
}
