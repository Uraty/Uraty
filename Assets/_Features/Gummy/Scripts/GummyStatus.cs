using System.Collections.Generic;
using UnityEngine;
using Uraty.Feature.Player;

namespace Uraty.Feature.Gummy
{
    [RequireComponent(typeof(Collider))]
    public sealed class GummyStatus : MonoBehaviour
    {
        private const string PlayerTag = "Player";
        private const float MinFollowDurationSeconds = 0.01f;
        private const float MinPlayerDetectIntervalSeconds = 0.1f;
        private const int MinItemScore = 0;
        private const int MaxDetectedPlayerColliderCount = 16;

        [Header("そのアイテムの得点")]
        [SerializeField] private int _itemScore = 1;

        [Header("追従開始距離")]
        [SerializeField] private float _reactionRangeMeters = 1.0f;

        [Header("追従時間")]
        [SerializeField] private float _followDurationSeconds = 0.5f;

        [Header("Collider が使えない場合の接触判定距離")]
        [SerializeField] private float _collectDistanceMeters = 0.1f;

        [Header("Player 検出間隔")]
        [SerializeField] private float _playerDetectIntervalSeconds = 0.1f;

        [Header("曲線追従の最大高さ")]
        [SerializeField] private float _followArcHeightMeters = 2.0f;

        [Header("Player 検出に使う LayerMask")]
        [SerializeField] private LayerMask _playerLayerMask = ~0;

        private readonly Collider[] _detectedPlayerColliders = new Collider[MaxDetectedPlayerColliderCount];
        private readonly List<Transform> _playersInRange = new List<Transform>(MaxDetectedPlayerColliderCount);
        private readonly HashSet<int> _playersInRangeInstanceIds = new HashSet<int>();

        private Vector3 _followStartPosition;
        private float _followElapsedSeconds;
        private float _playerDetectCooldownSeconds;

        private Transform _followTargetTransform;
        private Collider _selfCollider;
        private Collider _followTargetCollider;
        private PlayerStatus _followTargetPlayerStatus;

        private bool _isFollowing;

        public int ItemScore => _itemScore;
        public float ReactionRangeMeters => _reactionRangeMeters;

        private void OnValidate()
        {
            SanitizeSerializedFields();
        }

        private void Awake()
        {
            SanitizeSerializedFields();
            _selfCollider = GetComponent<Collider>();
            _playerDetectCooldownSeconds = 0.0f;
        }

        private void Update()
        {
            if (_isFollowing)
            {
                return;
            }

            UpdatePlayerDetectCooldown();
        }

        private void LateUpdate()
        {
            if (!_isFollowing)
            {
                return;
            }

            if (_followTargetTransform == null)
            {
                ResetFollowState();
                return;
            }

            FollowTarget();

            if (!HasReachedFollowTarget())
            {
                return;
            }

            AddScoreToFollowTargetPlayer();
            Collect();
        }

        private void SanitizeSerializedFields()
        {
            _itemScore = Mathf.Max(MinItemScore, _itemScore);
            _reactionRangeMeters = SanitizeNonNegativeFiniteValue(_reactionRangeMeters);
            _followDurationSeconds = SanitizePositiveFiniteValue(
                _followDurationSeconds,
                MinFollowDurationSeconds
            );
            _collectDistanceMeters = SanitizeNonNegativeFiniteValue(_collectDistanceMeters);
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
            Collider selectedPlayerCollider = null;
            PlayerStatus selectedPlayerStatus = null;

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
                selectedPlayerCollider = detectedPlayerCollider;

                if (playerTransform.TryGetComponent(out PlayerStatus playerStatus))
                {
                    selectedPlayerStatus = playerStatus;
                }
            }

            RefreshPlayersInRangeInstanceIds();

            if (selectedPlayerTransform == null)
            {
                return;
            }

            BeginFollow(selectedPlayerTransform, selectedPlayerCollider, selectedPlayerStatus);
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

        private void BeginFollow(
            Transform targetTransform,
            Collider targetCollider,
            PlayerStatus targetPlayerStatus
        )
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
            _followTargetCollider = targetCollider;
            _followTargetPlayerStatus = targetPlayerStatus;
            _followStartPosition = transform.position;
            _followElapsedSeconds = 0.0f;
            _isFollowing = true;
        }

        private void ResetFollowState()
        {
            _followElapsedSeconds = 0.0f;
            _followTargetTransform = null;
            _followTargetCollider = null;
            _followTargetPlayerStatus = null;
            _isFollowing = false;
            _playerDetectCooldownSeconds = 0.0f;

            _playersInRange.Clear();
            _playersInRangeInstanceIds.Clear();
        }

        private void FollowTarget()
        {
            if (_followTargetTransform == null)
            {
                return;
            }

            _followElapsedSeconds += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(_followElapsedSeconds / _followDurationSeconds);
            Vector3 targetPosition = _followTargetTransform.position;
            Vector3 linearPosition = Vector3.Lerp(_followStartPosition, targetPosition, normalizedTime);

            float arcHeightMeters = 4.0f * _followArcHeightMeters * normalizedTime * (1.0f - normalizedTime);
            transform.position = linearPosition + Vector3.up * arcHeightMeters;
        }

        private bool HasReachedFollowTarget()
        {
            if (_followTargetTransform == null)
            {
                return false;
            }

            if (_selfCollider != null && _followTargetCollider != null)
            {
                return _selfCollider.bounds.Intersects(_followTargetCollider.bounds);
            }

            Vector3 offset = _followTargetTransform.position - transform.position;
            float sqrDistance = offset.sqrMagnitude;
            float sqrCollectDistance = _collectDistanceMeters * _collectDistanceMeters;

            return sqrDistance <= sqrCollectDistance;
        }

        private void Collect()
        {
            Destroy(gameObject);
        }

        private void AddScoreToFollowTargetPlayer()
        {
            if (_followTargetPlayerStatus == null && _followTargetTransform != null)
            {
                _ = _followTargetTransform.TryGetComponent(out _followTargetPlayerStatus);
            }

            if (_followTargetPlayerStatus == null)
            {
                return;
            }

            _followTargetPlayerStatus.AddScore(_itemScore);
        }

        public int GetItemScore()
        {
            return _itemScore;
        }
    }
}