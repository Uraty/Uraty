using UnityEngine;


namespace Uraty.Feature.Generator
{
    [RequireComponent(typeof(Collider))]
    public sealed class DeliveryArea : MonoBehaviour
    {
        private const string PlayerTag = "Player";
        private const KeyCode DefaultDeliveryKey = KeyCode.E;

        [Header("納品キー")]
        [SerializeField] private KeyCode _deliveryKey = DefaultDeliveryKey;

        [Header("納品された累計スコア")]
        [SerializeField] private int _totalDeliveredScore;

        private Collider _triggerCollider;
        private IEmeraldScoreHolder _currentScoreHolder;

        public int TotalDeliveredScore => _totalDeliveredScore;

        private void Awake()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
        }

        private void Update()
        {
            if (_currentScoreHolder == null)
            {
                return;
            }

            if (!Input.GetKeyDown(_deliveryKey))
            {
                return;
            }

            DeliverEmeraldScore();
        }

        private void OnTriggerEnter(Collider other)
        {
            GameObject playerObject = GetPlayerObject(other);
            if (playerObject == null)
            {
                return;
            }

            if (!playerObject.CompareTag(PlayerTag))
            {
                return;
            }

            if (!TryGetEmeraldScoreHolder(playerObject, out IEmeraldScoreHolder scoreHolder))
            {
                return;
            }

            _currentScoreHolder = scoreHolder;
        }

        private void OnTriggerExit(Collider other)
        {
            GameObject playerObject = GetPlayerObject(other);
            if (playerObject == null)
            {
                return;
            }

            if (!playerObject.CompareTag(PlayerTag))
            {
                return;
            }

            if (!TryGetEmeraldScoreHolder(playerObject, out IEmeraldScoreHolder scoreHolder))
            {
                return;
            }

            if (_currentScoreHolder != scoreHolder)
            {
                return;
            }

            _currentScoreHolder = null;
        }

        private void DeliverEmeraldScore()
        {
            int deliveredScore = _currentScoreHolder.ConsumeHeldEmeraldScore();
            if (deliveredScore <= 0)
            {
                return;
            }

            _totalDeliveredScore += deliveredScore;
        }

        private static GameObject GetPlayerObject(Collider other)
        {
            if (other.attachedRigidbody != null)
            {
                return other.attachedRigidbody.gameObject;
            }

            return other.gameObject;
        }

        private static bool TryGetEmeraldScoreHolder(GameObject playerObject, out IEmeraldScoreHolder scoreHolder)
        {
            MonoBehaviour[] behaviours = playerObject.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IEmeraldScoreHolder emeraldScoreHolder)
                {
                    scoreHolder = emeraldScoreHolder;
                    return true;
                }
            }

            scoreHolder = null;
            return false;
        }
    }
}
