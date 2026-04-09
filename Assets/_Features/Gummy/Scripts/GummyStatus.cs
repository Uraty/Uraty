using UnityEngine;

namespace Uraty.Feature.Gummy
{
    // このオブジェクトに Collider が必須であることを示す
    [RequireComponent(typeof(Collider))]
    public sealed class GummyStatus : MonoBehaviour
    {
        // Player を検索するときに使用するタグ名
        private const string PlayerTag = "Player";

        // Player を再探索する最小間隔
        private const float MinPlayerSearchIntervalSeconds = 0.1f;

        // 追従時間の最小値
        private const float MinFollowDurationSeconds = 0.01f;

        // アイテムスコアの最小値
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

        // Inspector から直接 Player を設定できる参照
        [SerializeField] private Transform _playerTransform;

        // 追従開始時の座標
        private Vector3 _followStartPosition;

        // 追従開始からの経過時間
        private float _followElapsedSeconds;

        // 現在追従している対象
        private Transform _followTargetTransform;

        // 自身の Collider
        private Collider _selfCollider;

        // 追従対象の Collider
        private Collider _followTargetCollider;

        // Player 再探索までのクールダウン時間
        private float _playerSearchCooldownSeconds;

        // Tag 検索が可能かどうか
        private bool _canSearchPlayerByTag = true;

        // 現在追従中かどうか
        private bool _isFollowing;

        // 外部参照用プロパティ
        public int ItemScore => _itemScore;
        public float ReactionRangeMeters => _reactionRangeMeters;

        private void OnValidate()
        {
            // Inspector 上で値が変更されたときに不正値を補正する
            SanitizeSerializedFields();
        }

        private void Awake()
        {
            // 実行開始時にも不正値を補正する
            SanitizeSerializedFields();

            // 自身の Collider を取得
            _selfCollider = GetComponent<Collider>();

            // 再探索クールダウンを初期化
            _playerSearchCooldownSeconds = 0.0f;
        }

        private void Start()
        {
            // 開始時に Player を探す
            TryFindPlayerTransform();
        }

        private void Update()
        {
            // 既に追従中なら通常探索は行わない
            if (_isFollowing)
            {
                return;
            }

            // Player が未設定なら一定間隔で再探索する
            if (_playerTransform == null)
            {
                UpdatePlayerSearchCooldown();
                return;
            }

            // Player が反応距離内にいなければ何もしない
            if (!IsPlayerInReactionRange())
            {
                return;
            }

            // 反応距離内に入ったので追従開始
            BeginFollow(_playerTransform);
        }

        private void LateUpdate()
        {
            // 追従中でなければ処理しない
            if (!_isFollowing)
            {
                return;
            }

            // 追従対象が消えていたら処理できない
            if (_followTargetTransform == null)
            {
                return;
            }

            // 対象へ曲線追従する
            FollowTarget();

            // まだ対象に到達していないなら終了
            if (!HasReachedFollowTarget())
            {
                return;
            }

            // 到達したら回収処理
            Collect();
        }

        private void SanitizeSerializedFields()
        {
            // スコアを 0 以上に補正
            _itemScore = Mathf.Max(MinItemScore, _itemScore);

            // 負数や NaN などを防ぐ
            _reactionRangeMeters = SanitizeNonNegativeFiniteValue(_reactionRangeMeters);

            // 追従時間は最小値以上に補正
            _followDurationSeconds = SanitizePositiveFiniteValue(_followDurationSeconds, MinFollowDurationSeconds);

            // 接触距離を 0 以上に補正
            _collectDistanceMeters = SanitizeNonNegativeFiniteValue(_collectDistanceMeters);

            // Player 再探索間隔を最小値以上に補正
            _playerSearchIntervalSeconds = SanitizePositiveFiniteValue(
                _playerSearchIntervalSeconds,
                MinPlayerSearchIntervalSeconds
            );

            // 曲線の高さを 0 以上に補正
            _followArcHeightMeters = SanitizeNonNegativeFiniteValue(_followArcHeightMeters);
        }

        private float SanitizeNonNegativeFiniteValue(float value)
        {
            // NaN や Infinity は 0 に補正
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0.0f;
            }

            // 負数を 0 に補正
            return Mathf.Max(0.0f, value);
        }

        private float SanitizePositiveFiniteValue(float value, float minValue)
        {
            // NaN や Infinity は最低保証値に補正
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minValue;
            }

            // 最低保証値未満は補正
            return Mathf.Max(minValue, value);
        }

        private void UpdatePlayerSearchCooldown()
        {
            // Tag 検索が使えないなら再探索しない
            if (!_canSearchPlayerByTag)
            {
                return;
            }

            // クールダウンを減算
            _playerSearchCooldownSeconds -= Time.deltaTime;

            // まだ再探索タイミングでなければ待機
            if (_playerSearchCooldownSeconds > 0.0f)
            {
                return;
            }

            // 次の再探索タイミングをセットして検索
            _playerSearchCooldownSeconds = _playerSearchIntervalSeconds;
            TryFindPlayerTransform();
        }

        private void TryFindPlayerTransform()
        {
            // 既に Player が設定済みなら何もしない
            if (_playerTransform != null)
            {
                return;
            }

            // Tag 検索が無効なら何もしない
            if (!_canSearchPlayerByTag)
            {
                return;
            }

            try
            {
                // Player タグを持つオブジェクトを検索
                GameObject playerObject = GameObject.FindWithTag(PlayerTag);
                if (playerObject == null)
                {
                    return;
                }

                // 見つかった Player の Transform を保持
                _playerTransform = playerObject.transform;
            }
            catch (UnityException exception)
            {
                // Tag 未定義時は以降の Tag 検索を止める
                _canSearchPlayerByTag = false;

                // 警告を出して Inspector 設定を促す
                Debug.LogWarning(
                    $"Tag \"{PlayerTag}\" が未定義です。TagManager に追加するか、Inspector で _playerTransform を設定してください。\n{exception.Message}"
                );
            }
        }

        private bool IsPlayerInReactionRange()
        {
            // Player との距離を計算
            Vector3 offset = _playerTransform.position - transform.position;
            float sqrDistance = offset.sqrMagnitude;

            // 比較用に反応距離の二乗を計算
            float sqrReactionRange = _reactionRangeMeters * _reactionRangeMeters;

            // 実距離の平方根を使わず高速に判定
            return sqrDistance <= sqrReactionRange;
        }

        private void BeginFollow(Transform targetTransform)
        {
            // 既に追従中なら開始しない
            if (_isFollowing)
            {
                return;
            }

            // 対象が無効なら開始しない
            if (targetTransform == null)
            {
                return;
            }

            // 追従対象とその Collider を保持
            _followTargetTransform = targetTransform;
            _followTargetCollider = targetTransform.GetComponent<Collider>();

            // 追従開始位置と経過時間を初期化
            _followStartPosition = transform.position;
            _followElapsedSeconds = 0.0f;

            // 追従開始フラグを立てる
            _isFollowing = true;
        }

        private void FollowTarget()
        {
            // 対象が無効なら何もしない
            if (_followTargetTransform == null)
            {
                return;
            }

            // 経過時間を進める
            _followElapsedSeconds += Time.deltaTime;

            // 0～1 に正規化した進行度を求める
            float normalizedTime = Mathf.Clamp01(_followElapsedSeconds / _followDurationSeconds);

            // 開始位置から対象位置までを線形補間
            Vector3 targetPosition = _followTargetTransform.position;
            Vector3 linearPosition = Vector3.Lerp(_followStartPosition, targetPosition, normalizedTime);

            // 放物線状の高さを計算
            float arcHeightMeters = 4.0f * _followArcHeightMeters * normalizedTime * (1.0f - normalizedTime);

            // 線形移動に上方向の高さを足して曲線移動にする
            transform.position = linearPosition + Vector3.up * arcHeightMeters;
        }

        private bool HasReachedFollowTarget()
        {
            // 対象が無効なら未到達
            if (_followTargetTransform == null)
            {
                return false;
            }

            // 両者に Collider があれば bounds の交差で判定
            if (_selfCollider != null && _followTargetCollider != null)
            {
                return _selfCollider.bounds.Intersects(_followTargetCollider.bounds);
            }

            // Collider が使えない場合は距離で判定
            Vector3 offset = _followTargetTransform.position - transform.position;
            float sqrDistance = offset.sqrMagnitude;
            float sqrCollectDistance = _collectDistanceMeters * _collectDistanceMeters;

            return sqrDistance <= sqrCollectDistance;
        }

        private void Collect()
        {
            // 回収時は自身を削除する
            Destroy(gameObject);
        }

        public int GetItemScore()
        {
            // アイテムが持つスコアを返す
            return _itemScore;
        }
    }
}
