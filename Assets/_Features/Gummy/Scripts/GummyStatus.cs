using UnityEngine;

namespace Uraty.Feature.Gummy
{
    [RequireComponent(typeof(Collider))]
    public sealed class GummyStatus : MonoBehaviour
    {
        private const string PlayerTag = "Player";
        private const float MinPlayerSearchIntervalSeconds = 0.1f;
        private const float MinFollowDurationSeconds = 0.01f;
        private const int MinItemScore = 0;

        [Header("そのアイテムの得点")]
        [SerializeField] private int _itemScore = 1;

        [Header("追従開始距離")]
        [SerializeField] private float _reactionRangeMeters = 1.0f;

        [Header("追従時間")]
        [SerializeField] private float _followDurationSeconds = 0.5f;

        [Header("Collider が使えない場合の接触判定距離")]
        [SerializeField] private float _collectDistanceMeters = 0.1f;

        [Header("Player を再探索する間隔")]
        [SerializeField] private float _playerSearchIntervalSeconds = 1.0f;

        [Header("曲線追従の最大高さ")]
        [SerializeField] private float _followArcHeightMeters = 2.0f;

        [SerializeField] private Transform _playerTransform;

        private Vector3 _followStartPosition;
        private float _followElapsedSeconds;

        private Transform _followTargetTransform;
        private Collider _selfCollider;
        private Collider _followTargetCollider;

        private float _playerSearchCooldownSeconds;
        private bool _canSearchPlayerByTag = true;
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
            _playerSearchCooldownSeconds = 0.0f;
        }

        private void Start()
        {
            TryFindPlayerTransform();
        }

        private void Update()
        {
            if (_isFollowing)
            {
                return;
            }

            if (_playerTransform == null)
            {
                UpdatePlayerSearchCooldown();
                return;
            }

            if (!IsPlayerInReactionRange())
            {
                return;
            }

            BeginFollow(_playerTransform);
        }

        private void LateUpdate()
        {
            if (!_isFollowing)
            {
                return;
            }

            if (_followTargetTransform == null)
            {
                return;
            }

            FollowTarget();

            if (!HasReachedFollowTarget())
            {
                return;
            }

            Collect();
        }

        private void SanitizeSerializedFields()
        {
            _itemScore = Mathf.Max(MinItemScore, _itemScore);
            _reactionRangeMeters = SanitizeNonNegativeFiniteValue(_reactionRangeMeters);
            _followDurationSeconds = SanitizePositiveFiniteValue(_followDurationSeconds, MinFollowDurationSeconds);
            _collectDistanceMeters = SanitizeNonNegativeFiniteValue(_collectDistanceMeters);
            _playerSearchIntervalSeconds = SanitizePositiveFiniteValue(
                _playerSearchIntervalSeconds,
                MinPlayerSearchIntervalSeconds
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

        private void UpdatePlayerSearchCooldown()
        {
            if (!_canSearchPlayerByTag)
            {
                return;
            }

            _playerSearchCooldownSeconds -= Time.deltaTime;
            if (_playerSearchCooldownSeconds > 0.0f)
            {
                return;
            }

            _playerSearchCooldownSeconds = _playerSearchIntervalSeconds;
            TryFindPlayerTransform();
        }

        private void TryFindPlayerTransform()
        {
            if (_playerTransform != null)
            {
                return;
            }

            if (!_canSearchPlayerByTag)
            {
                return;
            }

            try
            {
                GameObject playerObject = GameObject.FindWithTag(PlayerTag);
                if (playerObject == null)
                {
                    return;
                }

                _playerTransform = playerObject.transform;
            }
            catch (UnityException exception)
            {
                _canSearchPlayerByTag = false;
                Debug.LogWarning(
                    $"Tag \"{PlayerTag}\" が未定義です。TagManager に追加するか、Inspector で _playerTransform を設定してください。\n{exception.Message}"
                );
            }
        }

        private bool IsPlayerInReactionRange()
        {
            Vector3 offset = _playerTransform.position - transform.position;
            float sqrDistance = offset.sqrMagnitude;
            float sqrReactionRange = _reactionRangeMeters * _reactionRangeMeters;

            return sqrDistance <= sqrReactionRange;
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
            _followTargetCollider = targetTransform.GetComponent<Collider>();

            _followStartPosition = transform.position;
            _followElapsedSeconds = 0.0f;
            _isFollowing = true;
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

        public int GetItemScore()
        {
            return _itemScore;
        }
    }
}
