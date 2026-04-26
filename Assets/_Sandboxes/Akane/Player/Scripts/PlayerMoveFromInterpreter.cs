using UnityEngine;

namespace Uraty.Feature.Player
{
    public sealed class PlayerMoveFromInterpreter : MonoBehaviour
    {
        private const float DefaultMoveSpeedMetersPerSecond = 5.0f;

        [SerializeField] private float _moveSpeedMetersPerSecond = DefaultMoveSpeedMetersPerSecond;
        [SerializeField] private PlayerInputInterpreter _inputInterpreter;

        private void Update()
        {
            if (_inputInterpreter == null)
            {
                return;
            }

            Vector3 moveDirection = _inputInterpreter.MoveDirectionWorld;
            transform.position += moveDirection * _moveSpeedMetersPerSecond * Time.deltaTime;
        }
    }
}
