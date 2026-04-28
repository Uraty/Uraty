using System.Collections.Generic;

using UnityEngine;

namespace Uraty.Features.Gummy
{
    [RequireComponent(typeof(Collider))]
    public sealed class GummyStatus : MonoBehaviour
    {
        // Player 判定に使うタグ名
        private const string PlayerTag = "Player";

        // 回収完了時間の最小値
        private const float MinCollectDurationSeconds = 0.01f;

        // Player 検出間隔の最小値
        private const float MinPlayerDetectIntervalSeconds = 0.1f;

        // 得点の最小値
        private const int MinItemScore = 0;

        // 一度に検出できる Player Collider 数の上限
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

        // OverlapSphereNonAlloc の検出結果を格納するバッファ
        private readonly Collider[] _detectedPlayerColliders =
            new Collider[MaxDetectedPlayerColliderCount];

        // 現在検出範囲にいたプレイヤー一覧
        private readonly List<Transform> _playersInRange =
            new List<Transform>(MaxDetectedPlayerColliderCount);

        // 直前フレームで検出済みだったプレイヤーの InstanceId
        private readonly HashSet<int> _playersInRangeInstanceIds = new HashSet<int>();

        // 追従開始地点
        private Vector3 _followStartPosition;

        // 回収演出の経過時間
        private float _collectElapsedSeconds;

        // 次回プレイヤー検出までのクールダウン
        private float _playerDetectCooldownSeconds;

        // 現在追従している対象プレイヤー
        private Transform _followTargetTransform;

        // 回収完了時点で記録しておく対象プレイヤー
        private Transform _completedTargetTransform;

        // 追従中かどうか
        private bool _isFollowing;

        // 回収完了済みかどうか
        private bool _isCollectionCompleted;

        // Application 側で回収処理を消費済みかどうか
        private bool _isCollectionConsumed;

        // 外部参照用プロパティ
        public int ItemScore => _itemScore;
        public float ReactionRangeMeters => _reactionRangeMeters;
        public bool IsCollectionCompleted => _isCollectionCompleted;

        private void OnValidate()
        {
            // Inspector 上の値を安全な範囲へ補正する
            SanitizeSerializedFields();
        }

        private void Awake()
        {
            // 起動時にも値を補正して不正値を防ぐ
            SanitizeSerializedFields();

            // 起動直後から検出可能なようにクールダウンを初期化する
            _playerDetectCooldownSeconds = 0.0f;
        }

        private void Update()
        {
            // 既に回収完了済みならこれ以上の処理は行わない
            if (_isCollectionCompleted)
            {
                return;
            }

            // 追従中は検出ではなく追従更新のみを行う
            if (_isFollowing)
            {
                UpdateFollowSequence();
                return;
            }

            // 待機中は一定間隔でプレイヤー検出を行う
            UpdatePlayerDetectCooldown();
        }

        private void SanitizeSerializedFields()
        {
            // 得点は 0 未満にならないようにする
            _itemScore = Mathf.Max(MinItemScore, _itemScore);

            // 各種設定値を安全な範囲へ補正する
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
            // NaN や Infinity は 0 として扱う
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0.0f;
            }

            // 0 未満にならないよう補正する
            return Mathf.Max(0.0f, value);
        }

        private float SanitizePositiveFiniteValue(float value, float minValue)
        {
            // NaN や Infinity は最小値へ置き換える
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minValue;
            }

            // 正の最小値を下回らないよう補正する
            return Mathf.Max(minValue, value);
        }

        private void UpdatePlayerDetectCooldown()
        {
            // プレイヤー検出クールダウンを減算する
            _playerDetectCooldownSeconds -= Time.deltaTime;
            if (_playerDetectCooldownSeconds > 0.0f)
            {
                return;
            }

            // 次回検出までの時間を再設定し、検出処理を行う
            _playerDetectCooldownSeconds = _playerDetectIntervalSeconds;
            TryBeginFollowByDetectedPlayer();
        }

        private void TryBeginFollowByDetectedPlayer()
        {
            // 毎回検出結果を作り直すためリストを初期化する
            _playersInRange.Clear();

            Transform selectedPlayerTransform = null;

            // 一定範囲内の Player Collider を取得する
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

                // 同一プレイヤーの重複登録を避ける
                if (_playersInRange.Contains(playerTransform))
                {
                    continue;
                }

                _playersInRange.Add(playerTransform);

                int playerInstanceId = playerTransform.GetInstanceID();

                // 直前検出済みのプレイヤーは再選択しない
                if (_playersInRangeInstanceIds.Contains(playerInstanceId))
                {
                    continue;
                }

                // 最初に見つけた新規プレイヤーだけを追従対象にする
                if (selectedPlayerTransform != null)
                {
                    continue;
                }

                selectedPlayerTransform = playerTransform;
            }

            // 今回検出したプレイヤー一覧を記録して次回判定に使う
            RefreshPlayersInRangeInstanceIds();

            if (selectedPlayerTransform == null)
            {
                return;
            }

            // 新規に見つかったプレイヤーへの追従を開始する
            BeginFollow(selectedPlayerTransform);
        }

        private Transform FindPlayerTransformFromCollider(Collider detectedPlayerCollider)
        {
            if (detectedPlayerCollider == null)
            {
                return null;
            }

            // 子 Collider に当たる場合もあるので親方向へ辿って Player タグを探す
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
            // 現在検出範囲にいるプレイヤーの InstanceId を記録する
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
            // 既に追従中なら開始しない
            if (_isFollowing)
            {
                return;
            }

            if (targetTransform == null)
            {
                return;
            }

            // 追従開始時点の状態を初期化する
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
            // 追従対象が消えた場合は状態をリセットする
            if (_followTargetTransform == null)
            {
                ResetFollowState();
                return;
            }

            // 回収演出の経過時間を進める
            _collectElapsedSeconds += Time.deltaTime;

            // 指定秒数でターゲットへ到達するよう補間率を計算する
            float normalizedTime = Mathf.Clamp01(_collectElapsedSeconds / _collectDurationSeconds);
            Vector3 targetPosition = _followTargetTransform.position;
            Vector3 linearPosition = Vector3.Lerp(_followStartPosition, targetPosition, normalizedTime);

            // 放物線状の高さを加算して見た目を調整する
            float arcHeightMeters =
                4.0f * _followArcHeightMeters * normalizedTime * (1.0f - normalizedTime);

            transform.position = linearPosition + Vector3.up * arcHeightMeters;

            // まだ指定秒数に達していなければ追従を継続する
            if (_collectElapsedSeconds < _collectDurationSeconds)
            {
                return;
            }

            // 指定秒数に達したら回収完了扱いにする
            CompleteCollection();
        }

        private void CompleteCollection()
        {
            // 完了時はターゲット位置へ揃えて見た目のズレを抑える
            if (_followTargetTransform != null)
            {
                transform.position = _followTargetTransform.position;
            }

            // Application 側へ渡す対象プレイヤーを保存する
            _completedTargetTransform = _followTargetTransform;

            // 追従状態を終了し、回収完了状態へ遷移する
            _followTargetTransform = null;
            _isFollowing = false;
            _isCollectionCompleted = true;
        }

        private void ResetFollowState()
        {
            // 追従関連の状態を初期値へ戻す
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

            // 回収完了済みかつ未処理のときだけ情報を引き渡す
            if (!_isCollectionCompleted || _isCollectionConsumed)
            {
                return false;
            }

            // 二重処理防止のため消費済みフラグを立てる
            _isCollectionConsumed = true;
            playerTransform = _completedTargetTransform;
            itemScore = _itemScore;
            return true;
        }

        public void DestroySelf()
        {
            // Application 側からの回収処理完了後に自分を破棄する
            Destroy(gameObject);
        }
    }
}

