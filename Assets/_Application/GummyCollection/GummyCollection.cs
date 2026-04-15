using UnityEngine;

using Uraty.Feature.Gummy;
using Uraty.Feature.Player;

namespace Uraty.Application
{
    public sealed class GummyCollection : MonoBehaviour
    {
        private const float MinMonitorIntervalSeconds = 0.01f;

        [Header("シーン全体の監視間隔")]
        [SerializeField] private float _monitorIntervalSeconds = 0.1f;

        private float _monitorCooldownSeconds;

        private void OnValidate()
        {
            _monitorIntervalSeconds = SanitizePositiveFiniteValue(
                _monitorIntervalSeconds,
                MinMonitorIntervalSeconds
            );
        }

        private void Awake()
        {
            _monitorIntervalSeconds = SanitizePositiveFiniteValue(
                _monitorIntervalSeconds,
                MinMonitorIntervalSeconds
            );
            _monitorCooldownSeconds = 0.0f;
        }

        private void Update()
        {
            _monitorCooldownSeconds -= Time.deltaTime;
            if (_monitorCooldownSeconds > 0.0f)
            {
                return;
            }

            _monitorCooldownSeconds = _monitorIntervalSeconds;
            ProcessCompletedCollections();
        }

        private void ProcessCompletedCollections()
        {
            GummyStatus[] gummyStatuses = FindObjectsByType<GummyStatus>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < gummyStatuses.Length; i++)
            {
                GummyStatus gummyStatus = gummyStatuses[i];
                if (gummyStatus == null)
                {
                    continue;
                }

                TryProcessCompletedCollection(gummyStatus);
            }
        }

        private void TryProcessCompletedCollection(GummyStatus gummyStatus)
        {
            if (!gummyStatus.TryConsumeCompletedCollection(out Transform playerTransform, out int itemScore))
            {
                return;
            }

            PlayerStatus playerStatus = FindPlayerStatusFromTransform(playerTransform);
            if (playerStatus != null)
            {
                playerStatus.ReceiveCollectedScore(itemScore);
            }

            gummyStatus.DestroySelf();
        }

        private PlayerStatus FindPlayerStatusFromTransform(Transform playerTransform)
        {
            Transform currentTransform = playerTransform;
            while (currentTransform != null)
            {
                if (currentTransform.TryGetComponent(out PlayerStatus playerStatus))
                {
                    return playerStatus;
                }

                currentTransform = currentTransform.parent;
            }

            return null;
        }

        private float SanitizePositiveFiniteValue(float value, float minValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minValue;
            }

            return Mathf.Max(minValue, value);
        }
    }
}
