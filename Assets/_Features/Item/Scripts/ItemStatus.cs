using UnityEngine;

namespace Uraty.Feature.Item
{
    public sealed class ItemStatus : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [Header("そのアイテムの得点")]
        [SerializeField] private int _itemScore = 1;

        [Header("当たり判定の範囲")]
        [SerializeField] private float _reactionRangeMeters = 1.0f;

        [SerializeField] private Transform _playerTransform;

        public int ItemScore => _itemScore;
        public float ReactionRangeMeters => _reactionRangeMeters;

        private void Awake()
        {
            _reactionRangeMeters = Mathf.Abs(_reactionRangeMeters);
        }

        private void Start()
        {
            if (_playerTransform != null)
            {
                return;
            }

            GameObject playerObject = GameObject.FindWithTag(PlayerTag);
            if (playerObject == null)
            {
                return;
            }

            _playerTransform = playerObject.transform;
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                return;
            }

            if (IsPlayerInReactionRange() == false)
            {
                return;
            }

            Collect();
        }

        private bool IsPlayerInReactionRange()
        {
            Vector3 offset = _playerTransform.position - transform.position;
            float sqrDistance = offset.sqrMagnitude;
            float sqrReactionRange = _reactionRangeMeters * _reactionRangeMeters;

            return sqrDistance <= sqrReactionRange;
        }

        private void Collect()
        {
            Destroy(gameObject);
        }
    }
}
