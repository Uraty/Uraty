using System;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterMove : MonoBehaviour
    {
        private const float MinMoveDirectionSqrMagnitude = 0.0001f;

        [SerializeField]
        private CharacterController _characterController;

        [SerializeField]
        private float _moveSpeed = 10.0f;

        [SerializeField]
        private float _rotationSpeedDegrees = 720.0f;

        private void Reset()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_characterController == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(CharacterController)} が設定されていません。");
            }
        }

        public void Initialize(float moveSpeed)
        {
            _moveSpeed = moveSpeed;
        }

        public void Move(Vector3 moveDirectionWorld)
        {
            moveDirectionWorld.y = 0.0f;

            if (moveDirectionWorld.sqrMagnitude <= MinMoveDirectionSqrMagnitude)
            {
                return;
            }

            if (moveDirectionWorld.sqrMagnitude > 1.0f)
            {
                moveDirectionWorld.Normalize();
            }

            Rotate(moveDirectionWorld);

            _characterController.Move(
                moveDirectionWorld * _moveSpeed * Time.deltaTime);
        }

        private void Rotate(Vector3 moveDirectionWorld)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                moveDirectionWorld,
                Vector3.up);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeedDegrees * Time.deltaTime);
        }
    }
}
