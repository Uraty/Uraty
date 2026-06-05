using System.Collections;

using R3;

using TMPro;

using UnityEngine;

using Uraty.Shared.Team;

namespace Uraty.Application.Mode
{
    /// <summary>
    /// WantedSystem からチームスコアを取得して UI に表示するビューです。
    /// キャラクター登録が完了した後のスコアを基準（0）として差分表示します。
    /// </summary>
    public sealed class ScoreUI : MonoBehaviour
    {
        [Header("System")]
        [SerializeField]
        private WantedSystem _wantedSystem;

        [Header("UI")]
        [SerializeField]
        private TextMeshProUGUI _primaryScoreText;

        [SerializeField]
        private TextMeshProUGUI _secondaryScoreText;

        // --- 基準スコア（登録完了後の値）---
        private int _primaryBaseline;
        private int _secondaryBaseline;

        private DisposableBag _disposables;

        private IEnumerator Start()
        {
            if (_wantedSystem == null)
            {
                Debug.LogError(
                    $"{nameof(ScoreUI)} に WantedSystem が設定されていません。");

                yield break;
            }

            // WantedSystem がキャラクター登録を完了するまで待ちます
            // （登録完了 = いずれかのチームスコアが 0 より大きくなる）
            yield return new WaitUntil(() =>
                _wantedSystem.GetTeamScoreOrDefault(TeamId.Primary) > 0
                || _wantedSystem.GetTeamScoreOrDefault(TeamId.Secondary) > 0);

            // この時点で全登録イベントが完了しています。
            // その値を基準として 0 表示とします。
            _primaryBaseline = _wantedSystem.GetTeamScoreOrDefault(TeamId.Primary);
            _secondaryBaseline = _wantedSystem.GetTeamScoreOrDefault(TeamId.Secondary);

            SetText(_primaryScoreText, 0);
            SetText(_secondaryScoreText, 0);

            // 基準設定後のスコア変化のみ購読します
            _wantedSystem.TeamScoreChangedStream
                .Subscribe(OnTeamScoreChanged)
                .AddTo(ref _disposables);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }

        /// <summary>
        /// チームスコアが変化したときに呼ばれます。
        /// 基準値との差分を表示します。
        /// </summary>
        private void OnTeamScoreChanged(TeamId teamId)
        {
            int raw = _wantedSystem.GetTeamScoreOrDefault(teamId);

            if (teamId == TeamId.Primary)
            {
                SetText(_primaryScoreText, raw - _primaryBaseline);
            }
            else if (teamId == TeamId.Secondary)
            {
                SetText(_secondaryScoreText, raw - _secondaryBaseline);
            }
        }

        private static void SetText(TextMeshProUGUI label, int value)
        {
            if (label == null)
            {
                return;
            }

            label.text = value.ToString();
        }
    }
}
