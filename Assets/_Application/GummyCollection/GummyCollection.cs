using UnityEngine;

using Uraty.Feature.Gummy;
using Uraty.Feature.Player;

namespace Uraty.Application
{
    public sealed class GummyCollection : MonoBehaviour
    {
        // 監視間隔の下限値
        private const float MinMonitorIntervalSeconds = 0.01f;

        [Header("シーン全体の監視間隔")]
        [SerializeField] private float _monitorIntervalSeconds = 0.1f;

        // 次回監視までのクールダウン
        private float _monitorCooldownSeconds;

        private void OnValidate()
        {
            // Inspector で不正な値が入っても、最小値以上になるよう補正する
            _monitorIntervalSeconds = SanitizePositiveFiniteValue(
                _monitorIntervalSeconds,
                MinMonitorIntervalSeconds
            );
        }

        private void Awake()
        {
            // 起動時にも監視間隔を補正して安全な状態にする
            _monitorIntervalSeconds = SanitizePositiveFiniteValue(
                _monitorIntervalSeconds,
                MinMonitorIntervalSeconds
            );

            // 起動直後から監視できるようにクールダウンを初期化する
            _monitorCooldownSeconds = 0.0f;
        }

        private void Update()
        {
            // 監視クールダウンを減算する
            _monitorCooldownSeconds -= Time.deltaTime;
            if (_monitorCooldownSeconds > 0.0f)
            {
                return;
            }

            // 次回監視までの時間を再設定する
            _monitorCooldownSeconds = _monitorIntervalSeconds;

            // シーン内のガム回収完了状態を監視して処理する
            ProcessCompletedCollections();
        }

        private void ProcessCompletedCollections()
        {
            // シーン内に存在する有効な GummyStatus を全取得する
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

                // 各ガムの回収完了状態を個別に処理する
                TryProcessCompletedCollection(gummyStatus);
            }
        }

        private void TryProcessCompletedCollection(GummyStatus gummyStatus)
        {
            // 回収完了済みで、まだ未処理のガムだけを処理対象にする
            if (!gummyStatus.TryConsumeCompletedCollection(out Transform playerTransform, out int itemScore))
            {
                return;
            }

            // 回収対象だったプレイヤーから PlayerStatus を特定する
            PlayerStatus playerStatus = FindPlayerStatusFromTransform(playerTransform);
            if (playerStatus != null)
            {
                // プレイヤーへガムの得点を加算する
                playerStatus.ReceiveCollectedScore(itemScore);
            }

            // Application 側で処理完了後にガム本体を破棄する
            gummyStatus.DestroySelf();
        }

        private PlayerStatus FindPlayerStatusFromTransform(Transform playerTransform)
        {
            // プレイヤーの子オブジェクトに当たる場合もあるので、親方向へ辿って PlayerStatus を探す
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
            // NaN や Infinity の場合は最小値に置き換える
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minValue;
            }

            // 正の最小値を下回らないように補正する
            return Mathf.Max(minValue, value);
        }
    }
}
