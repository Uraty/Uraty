using System;
using System.Collections;
using System.Reflection;

using R3;

using TMPro;

using UnityEngine;

using Uraty.Shared.Team;

namespace Uraty.Application.Battle
{
    /// <summary>
    /// WantedSystem からチームスコアを取得して UI に表示するビューです。
    /// WantedSystem との循環参照を避けるため Reflection でアクセスします。
    /// キャラクター登録が完了した後のスコアを基準（0）として差分表示します。
    /// </summary>
    public sealed class ScoreView : MonoBehaviour
    {
        /// <summary>
        /// WantedSystem の完全修飾型名です。
        /// </summary>
        private const string WantedSystemTypeName =
            "Uraty.Application.Mode.WantedSystem";

        /// <summary>
        /// スコアのソースとなる WantedSystem MonoBehaviour です。
        /// </summary>
        [Header("System")]
        [SerializeField]
        private MonoBehaviour _wantedSystem;

        /// <summary>
        /// Primary チームのスコアを表示する TextMeshProUGUI です。
        /// </summary>
        [Header("UI")]
        [SerializeField]
        private TextMeshProUGUI _primaryScoreText;

        /// <summary>
        /// Secondary チームのスコアを表示する TextMeshProUGUI です。
        /// </summary>
        [SerializeField]
        private TextMeshProUGUI _secondaryScoreText;

        // --- Reflection キャッシュ ---

        private static Type _cachedType;
        private static MethodInfo _cachedGetTeamScoreOrDefault;
        private static PropertyInfo _cachedTeamScoreChangedStream;
        private static bool _isReflectionCached;

        // --- 基準スコア（登録完了後の値）---

        private int _primaryBaseline;
        private int _secondaryBaseline;

        /// <summary>
        /// R3 購読の破棄に使用する DisposableBag です。
        /// </summary>
        private DisposableBag _disposables;

        /// <summary>
        /// WantedSystem がキャラクター登録を終えてから基準を取得し、
        /// その後のスコア変化のみ購読します。
        /// </summary>
        private IEnumerator Start()
        {
            if (_wantedSystem == null)
            {
                Debug.LogError(
                    $"{nameof(ScoreView)} に WantedSystem が設定されていません。");

                yield break;
            }

            if (!EnsureReflectionCached())
            {
                yield break;
            }

            // WantedSystem がキャラクター登録を完了するまで待ちます
            // （登録完了 = いずれかのチームスコアが 0 より大きくなる）
            yield return new WaitUntil(() =>
                GetRawScore(TeamId.Primary) > 0
                || GetRawScore(TeamId.Secondary) > 0);

            // この時点でフレームをまたいでいるため、
            // 同フレームに行われた全登録イベントが完了しています。
            // その値を基準として 0 表示とします。
            _primaryBaseline =
                GetRawScore(TeamId.Primary);

            _secondaryBaseline =
                GetRawScore(TeamId.Secondary);

            SetText(_primaryScoreText, 0);
            SetText(_secondaryScoreText, 0);

            // 基準設定後のスコア変化のみ購読します
            Observable<TeamId> observable =
                _cachedTeamScoreChangedStream.GetValue(_wantedSystem)
                    as Observable<TeamId>;

            if (observable == null)
            {
                Debug.LogError(
                    $"{nameof(ScoreView)}: TeamScoreChangedStream の取得に失敗しました。");

                yield break;
            }

            observable
                .Subscribe(teamId => OnTeamScoreChanged(teamId))
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
            int raw = GetRawScore(teamId);

            if (teamId == TeamId.Primary)
            {
                SetText(_primaryScoreText, raw - _primaryBaseline);
            }
            else if (teamId == TeamId.Secondary)
            {
                SetText(_secondaryScoreText, raw - _secondaryBaseline);
            }
        }

        /// <summary>
        /// Reflection で WantedSystem からチームの生スコアを取得します。
        /// </summary>
        private int GetRawScore(TeamId teamId)
        {
            if (_cachedGetTeamScoreOrDefault == null
                || _wantedSystem == null)
            {
                return 0;
            }

            return (int)_cachedGetTeamScoreOrDefault.Invoke(
                _wantedSystem,
                new object[] { teamId });
        }

        private static void SetText(TextMeshProUGUI label, int value)
        {
            if (label == null)
            {
                return;
            }

            label.text = value.ToString();
        }

        /// <summary>
        /// Reflection キャッシュを初期化します。
        /// </summary>
        private static bool EnsureReflectionCached()
        {
            if (_isReflectionCached)
            {
                return _cachedType != null;
            }

            _isReflectionCached = true;

            foreach (System.Reflection.Assembly assembly
                     in AppDomain.CurrentDomain.GetAssemblies())
            {
                _cachedType = assembly.GetType(WantedSystemTypeName);

                if (_cachedType != null)
                {
                    break;
                }
            }

            if (_cachedType == null)
            {
                Debug.LogError(
                    $"{nameof(ScoreView)}: '{WantedSystemTypeName}' が見つかりません。");

                return false;
            }

            _cachedGetTeamScoreOrDefault =
                _cachedType.GetMethod(
                    "GetTeamScoreOrDefault",
                    BindingFlags.Public | BindingFlags.Instance);

            _cachedTeamScoreChangedStream =
                _cachedType.GetProperty(
                    "TeamScoreChangedStream",
                    BindingFlags.Public | BindingFlags.Instance);

            if (_cachedGetTeamScoreOrDefault == null
                || _cachedTeamScoreChangedStream == null)
            {
                Debug.LogError(
                    $"{nameof(ScoreView)}: WantedSystem のメンバー取得に失敗しました。");

                return false;
            }

            return true;
        }
    }
}
