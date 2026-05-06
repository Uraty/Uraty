using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterDetect : MonoBehaviour
    {
        [SerializeField]
        private CharacterStatus _characterStatus;

        [SerializeField]
        private LayerMask _bushLayer;

        [SerializeField, Min(0.001f)]
        private float _checkRadius = 0.05f;

        private void Update()
        {
            bool isInsideBush = Physics.CheckSphere(
                transform.position,
                _checkRadius,
                _bushLayer,
                QueryTriggerInteraction.Collide);

            _characterStatus.SetInsideBush(isInsideBush);
        }
    }
}
