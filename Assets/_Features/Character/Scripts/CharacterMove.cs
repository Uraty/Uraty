using System;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterMove : MonoBehaviour
    {
        private const float MinMoveDirectionSqrMagnitude = 0.0001f;
        private const string IsMovingParameterName = "IsMoving";
        private const float MovingHoldSeconds = 0.1f;

        [SerializeField]
        private CharacterController _characterController;

        [SerializeField]
        private CharacterStatus _status;

        [SerializeField]
        private float _moveSpeed = 10.0f;

        private float _lastMoveTime = -1.0f;
        private bool _currentIsMoving;

        private bool _isSkillMoving;
        private Vector3 _skillMoveDirection = Vector3.forward;
        private float _skillMoveSpeed;
        private float _skillMoveRemainingDistance;

        public bool IsSkillMoving => _isSkillMoving;

        private void Reset()
        {
            _characterController = GetComponent<CharacterController>();
            _status = GetComponent<CharacterStatus>();
        }

        private void Awake()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_status == null)
            {
                _status = GetComponent<CharacterStatus>();
            }

            if (_characterController == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(CharacterController)} が設定されていません。");
            }

            if (_status == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(CharacterStatus)} が設定されていません。");
            }

            if (!HasBoolParameter(_status.Animator, IsMovingParameterName))
            {
                throw new InvalidOperationException(
                    $"{nameof(Animator)} に Bool パラメータ {IsMovingParameterName} が存在しません。");
            }
        }

        private void Update()
        {
            UpdateSkillMove();
            UpdateMoveAnimation();
        }

        private void OnDisable()
        {
            EndSkillMove();

            if (_status.Animator != null)
            {
                _status.Animator.SetBool(IsMovingParameterName, false);
            }

            _currentIsMoving = false;
            _lastMoveTime = -1.0f;
        }

        public void Initialize(float moveSpeed)
        {
            _moveSpeed = moveSpeed;
        }

        public void Move(Vector3 moveDirectionWorld)
        {
            if (_isSkillMoving)
            {
                return;
            }

            moveDirectionWorld.y = 0.0f;

            if (moveDirectionWorld.sqrMagnitude <= MinMoveDirectionSqrMagnitude)
            {
                return;
            }

            if (moveDirectionWorld.sqrMagnitude > 1.0f)
            {
                moveDirectionWorld.Normalize();
            }

            _lastMoveTime = Time.time;

            Rotate(moveDirectionWorld);

            _characterController.Move(
                moveDirectionWorld * _moveSpeed * Time.deltaTime);
        }

        public bool BeginSkillMove(
            Vector3 moveDirectionWorld,
            float speed,
            float distance)
        {
            if (_status == null || !_status.IsAlive)
            {
                return false;
            }

            moveDirectionWorld.y = 0.0f;

            if (moveDirectionWorld.sqrMagnitude <= MinMoveDirectionSqrMagnitude)
            {
                return false;
            }

            float validSpeed = Mathf.Max(0.0f, speed);
            float validDistance = Mathf.Max(0.0f, distance);

            if (validSpeed <= 0.0f || validDistance <= 0.0f)
            {
                EndSkillMove();
                return false;
            }

            _skillMoveDirection = moveDirectionWorld.normalized;
            _skillMoveSpeed = validSpeed;
            _skillMoveRemainingDistance = validDistance;
            _isSkillMoving = true;

            Rotate(_skillMoveDirection);

            return true;
        }

        private void UpdateSkillMove()
        {
            if (!_isSkillMoving)
            {
                return;
            }

            if (_status == null || !_status.IsAlive)
            {
                EndSkillMove();
                return;
            }

            float moveDistance = Mathf.Min(
                _skillMoveSpeed * Time.deltaTime,
                _skillMoveRemainingDistance);

            if (moveDistance <= 0.0f)
            {
                EndSkillMove();
                return;
            }

            Rotate(_skillMoveDirection);

            _characterController.Move(
                _skillMoveDirection * moveDistance);

            _skillMoveRemainingDistance -= moveDistance;

            if (_skillMoveRemainingDistance <= 0.0f)
            {
                EndSkillMove();
            }
        }

        private void EndSkillMove()
        {
            _isSkillMoving = false;
            _skillMoveRemainingDistance = 0.0f;
            _skillMoveSpeed = 0.0f;
        }

        private void UpdateMoveAnimation()
        {
            bool isMoving = Time.time - _lastMoveTime <= MovingHoldSeconds;

            if (_currentIsMoving == isMoving)
            {
                return;
            }

            _currentIsMoving = isMoving;
            _status.Animator.SetBool(IsMovingParameterName, _currentIsMoving);
        }

        private void Rotate(Vector3 moveDirectionWorld)
        {
            transform.rotation = Quaternion.LookRotation(
                moveDirectionWorld,
                Vector3.up);
        }

        private static bool HasBoolParameter(Animator animator, string parameterName)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName &&
                    parameter.type == AnimatorControllerParameterType.Bool)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
