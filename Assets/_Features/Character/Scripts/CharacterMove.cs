using System;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterMove : MonoBehaviour
    {
        [SerializeField] private CharacterController _characterController;

        private float _moveSpeed = 20;

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

            if (moveDirectionWorld.sqrMagnitude > 1.0f)
            {
                moveDirectionWorld.Normalize();
            }

            _characterController.Move(
                moveDirectionWorld * _moveSpeed * Time.deltaTime);
        }
    }
}
