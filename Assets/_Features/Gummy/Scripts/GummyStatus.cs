using System.Collections.Generic;

using UnityEngine;

namespace Uraty.Feature.Gummy
{
    [RequireComponent(typeof(Collider))]
    public sealed class GummyStatus : MonoBehaviour
    {
        private const string PlayerTag = "Player";
        private const float MinCollectDurationSeconds = 0.01f;
        private const float MinPlayerDetectIntervalSeconds = 0.1f;
        private const int MinItemScore = 0;
        private const int MaxDetectedPlayerColliderCount = 16;

        [Header("そのアイテムの得点")]
        [SerializeField] private int _itemScore = 1;

        [Header("追従開始距離")]
        [SerializeField] private float _reactionRangeMeters = 1.0f;

        [Header("回収完了までの秒数")]
        [SerializeField] private float _collectDurationSeconds = 0.5f;

        [Header("Player 検出間隔")]
        [SerializeField] private float _playerDetectIntervalSeconds = 0.1f;

        [Header("曲線追従の最大高さ")]
        [SerializeField] private float _followArcHeightMeters = 2.0f;

        [Header("Player 検出に使う LayerMask")]
        [SerializeField] private LayerMask _playerLayerMask = ~0;

        private readonly Collider[] _detectedPlayerColliders =
            new Collider[MaxDetectedPlayerColliderCount];

        private readonly List<Transform> _playersInRange =
            new List<Transform>(MaxDetectedPlayerColliderCount);

        private readonly HashSet<int> _playersInRangeInstanceIds = new HashSet<int>();

        private Vector3 _followStartPosition;
        private float _collectElapsedSeconds;
        private float _playerDetectCooldownSeconds;

        private Transform _followTargetTransform;
        private Transform _completedTargetTransform;

        private bool _isFollowing;
        private bool _isCollectionCompleted;
        private bool _isCollectionConsumed;

        public int ItemScore => _itemScore;
        public float ReactionRangeMeters => _reactionRangeMeters;
        public bool IsCollectionCompleted => _isCollectionCompleted;

        private void OnValidate()
        {
            SanitizeSerializedFields();
        }

        private void Awake()
        {
            SanitizeSerializedFields();
            _playerDetectCooldownSeconds = 0.0f;
        }

        private void Update()
        {
            if (_isCollectionCompleted)
            {
                return;
            }

            if (_isFollowing)
            {
                UpdateFollowSequence();
                return;
            }

            UpdatePlayerDetectCooldown();
        }

        private void SanitizeSerializedFields()
        {
            _itemScore = Mathf.Max(MinItemScore, _itemScore);
            _reactionRangeMeters = SanitizeNonNegativeFiniteValue(_reactionRangeMeters);
            _collectDurationSeconds = SanitizePositiveFiniteValue(
                _collectDurationSeconds,
                MinCollectDurationSeconds
            );
            _playerDetectIntervalSeconds = SanitizePositiveFiniteValue(
                _playerDetectIntervalSeconds,
                MinPlayerDetectIntervalSeconds
            );
            _followArcHeightMeters = SanitizeNonNegativeFiniteValue(_followArcHeightMeters);
        }

        private float SanitizeNonNegativeFiniteValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0.0f;
            }

            return Mathf.Max(0.0f, value);
        }

        private float SanitizePositiveFiniteValue(float value, float minValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minValue;
            }

            return Mathf.Max(minValue, value);
        }

        private void UpdatePlayerDetectCooldown()
        {
            _playerDetectCooldownSeconds -= Time.deltaTime;
            if (_playerDetectCooldownSeconds > 0.0f)
            {
                return;
            }

            _playerDetectCooldownSeconds = _playerDetectIntervalSeconds;
            TryBeginFollowByDetectedPlayer();
        }

        private void TryBeginFollowByDetectedPlayer()
        {
            _playersInRange.Clear();

            Transform selectedPlayerTransform = null;

            int detectedPlayerColliderCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                _reactionRangeMeters,
                _detectedPlayerColliders,
                _playerLayerMask,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < detectedPlayerColliderCount; i++)
            {
                Collider detectedPlayerCollider = _detectedPlayerColliders[i];
                Transform playerTransform = FindPlayerTransformFromCollider(detectedPlayerCollider);
                if (playerTransform == null)
                {
                    continue;
                }

                if (_playersInRange.Contains(playerTransform))
                {
                    continue;
                }

                _playersInRange.Add(playerTransform);

                int playerInstanceId = playerTransform.GetInstanceID();
                if (_playersInRangeInstanceIds.Contains(playerInstanceId))
                {
                    continue;
                }

                if (selectedPlayerTransform != null)
                {
                    continue;
                }

                selectedPlayerTransform = playerTransform;
            }

            RefreshPlayersInRangeInstanceIds();

            if (selectedPlayerTransform == null)
            {
                return;
            }

            BeginFollow(selectedPlayerTransform);
        }

        private Transform FindPlayerTransformFromCollider(Collider detectedPlayerCollider)
        {
            if (detectedPlayerCollider == null)
            {
                return null;
            }

            Transform currentTransform = detectedPlayerCollider.transform;
            while (currentTransform != null)
            {
                if (currentTransform.CompareTag(PlayerTag))
                {
                    return currentTransform;
                }

                currentTransform = currentTransform.parent;
            }

            return null;
        }

        private void RefreshPlayersInRangeInstanceIds()
        {
            _playersInRangeInstanceIds.Clear();

            for (int i = 0; i < _playersInRange.Count; i++)
            {
                Transform playerTransform = _playersInRange[i];
                if (playerTransform == null)
                {
                    continue;
                }

                _playersInRangeInstanceIds.Add(playerTransform.GetInstanceID());
            }
        }

        private void BeginFollow(Transform targetTransform)
        {
            if (_isFollowing)
            {
                return;
            }

            if (targetTransform == null)
            {
                return;
            }

            _followTargetTransform = targetTransform;
            _followStartPosition = transform.position;
            _collectElapsedSeconds = 0.0f;
            _completedTargetTransform = null;
            _isCollectionCompleted = false;
            _isCollectionConsumed = false;
            _isFollowing = true;
        }

        private void UpdateFollowSequence()
        {
            if (_followTargetTransform == null)
            {
                ResetFollowState();
                return;
            }

            _collectElapsedSeconds += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(_collectElapsedSeconds / _collectDurationSeconds);
            Vector3 targetPosition = _followTargetTransform.position;
            Vector3 linearPosition = Vector3.Lerp(_followStartPosition, targetPosition, normalizedTime);

            float arcHeightMeters =
                4.0f * _followArcHeightMeters * normalizedTime * (1.0f - normalizedTime);

            transform.position = linearPosition + Vector3.up * arcHeightMeters;

            if (_collectElapsedSeconds < _collectDurationSeconds)
            {
                return;
            }

            CompleteCollection();
        }

        private void CompleteCollection()
        {
            if (_followTargetTransform != null)
            {
                transform.position = _followTargetTransform.position;
            }

            _completedTargetTransform = _followTargetTransform;
            _followTargetTransform = null;
            _isFollowing = false;
            _isCollectionCompleted = true;
        }

        private void ResetFollowState()
        {
            _collectElapsedSeconds = 0.0f;
            _followTargetTransform = null;
            _completedTargetTransform = null;
            _isFollowing = false;
            _isCollectionCompleted = false;
            _isCollectionConsumed = false;
            _playerDetectCooldownSeconds = 0.0f;

            _playersInRange.Clear();
            _playersInRangeInstanceIds.Clear();
        }

        public bool TryConsumeCompletedCollection(out Transform playerTransform, out int itemScore)
        {
            playerTransform = null;
            itemScore = 0;

            if (!_isCollectionCompleted || _isCollectionConsumed)
            {
                return false;
            }

            _isCollectionConsumed = true;
            playerTransform = _completedTargetTransform;
            itemScore = _itemScore;
            return true;
        }

        public void DestroySelf()
        {
            Destroy(gameObject);
        }
    }
}
