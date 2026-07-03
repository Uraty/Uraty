using System;
using System.Collections;
using System.Collections.Generic;

using R3;

using UnityEngine;

using Uraty.Application.Battle;
using Uraty.Features.Character;
using Uraty.Shared.Entry;
using Uraty.Shared.Team;

namespace Uraty.Application.Mode
{
    /// <summary>
    /// Wanted モードにおける Character ごとのスコア移動を管理します。
    /// </summary>
    public sealed class WantedSystem : MonoBehaviour
    {
        [Header("Battle")]
        [SerializeField]
        private BattleApplication _battleApplication;

        [Header("Result Entry")]
        [SerializeField]
        private ResultSceneEntry _resultSceneEntry;

        [Header("Score")]
        [SerializeField, Min(0)]
        private int _initialScore = 1;

        private readonly Dictionary<CharacterStatus, int> _scoreByStatus = new();

        private readonly Dictionary<TeamId, int> _scoreByTeamId = new();

        private readonly Dictionary<CharacterStatus, IDisposable>
            _killedSubscriptionByStatus = new();

        private readonly Dictionary<CharacterStatus, int>
            _characterIndexByStatus = new();

        private readonly Subject<CharacterStatus> _scoreChangedSubject = new();

        private readonly Subject<TeamId> _teamScoreChangedSubject = new();

        /// <summary>
        /// スコアが変更された CharacterStatus を通知します。
        /// </summary>
        public Observable<CharacterStatus> ScoreChangedStream => _scoreChangedSubject;

        /// <summary>
        /// 合計スコアが変更された TeamId を通知します。
        /// </summary>
        public Observable<TeamId> TeamScoreChangedStream => _teamScoreChangedSubject;

        private IEnumerator Start()
        {
            if (_battleApplication == null)
            {
                Debug.LogError(
                    $"{nameof(WantedSystem)} に {nameof(BattleApplication)} が設定されていません。");

                yield break;
            }

            yield return new WaitUntil(
                HasSpawnedCharacter);

            RegisterCharactersFromBattleApplication();
            WriteModeResultToEntry();
        }

        /// <summary>
        /// CharacterStatus を Wanted のスコア管理対象として登録します。
        /// </summary>
        /// <param name="status">登録対象の CharacterStatus です。</param>
        public void RegisterCharacter(CharacterStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (_scoreByStatus.ContainsKey(status))
            {
                return;
            }

            int initialScore = GetInitialScore();

            _scoreByStatus.Add(status, initialScore);

            ApplyTeamScoreDelta(
                status.TeamId,
                initialScore);

            IDisposable killedSubscription =
                status.KilledStream
                    .Subscribe(killerStatus =>
                    {
                        HandleCharacterKilled(
                            status,
                            killerStatus);
                    });

            _killedSubscriptionByStatus.Add(
                status,
                killedSubscription);

            PublishScoreChanged(status);
            WriteCharacterScoreToEntry(status);
            WriteModeResultToEntry();
        }

        /// <summary>
        /// CharacterStatus を Wanted のスコア管理対象から解除します。
        /// </summary>
        /// <param name="status">解除対象の CharacterStatus です。</param>
        public void UnregisterCharacter(CharacterStatus status)
        {
            if (status == null)
            {
                return;
            }

            if (_killedSubscriptionByStatus.TryGetValue(
                    status,
                    out IDisposable killedSubscription))
            {
                killedSubscription.Dispose();

                _killedSubscriptionByStatus.Remove(status);
            }

            if (_scoreByStatus.TryGetValue(
                    status,
                    out int score))
            {
                ApplyTeamScoreDelta(
                    status.TeamId,
                    -score);
            }

            _scoreByStatus.Remove(status);
            _characterIndexByStatus.Remove(status);

            WriteModeResultToEntry();
        }

        /// <summary>
        /// CharacterStatus の現在スコアを取得します。
        /// </summary>
        /// <param name="status">取得対象の CharacterStatus です。</param>
        /// <param name="score">現在スコアです。</param>
        /// <returns>スコアを取得できた場合は true です。</returns>
        public bool TryGetScore(
            CharacterStatus status,
            out int score)
        {
            if (status == null)
            {
                score = 0;
                return false;
            }

            return _scoreByStatus.TryGetValue(
                status,
                out score);
        }

        /// <summary>
        /// CharacterStatus の現在スコアを取得します。
        /// 未登録の場合は 0 を返します。
        /// </summary>
        /// <param name="status">取得対象の CharacterStatus です。</param>
        /// <returns>現在スコアです。</returns>
        public int GetScoreOrDefault(CharacterStatus status)
        {
            if (status == null)
            {
                return 0;
            }

            return _scoreByStatus.TryGetValue(
                status,
                out int score)
                ? score
                : 0;
        }

        /// <summary>
        /// TeamId の合計スコアを取得します。
        /// </summary>
        /// <param name="teamId">取得対象の TeamId です。</param>
        /// <param name="score">合計スコアです。</param>
        /// <returns>スコアを取得できた場合は true です。</returns>
        public bool TryGetTeamScore(
            TeamId teamId,
            out int score)
        {
            if (teamId == TeamId.None)
            {
                score = 0;
                return false;
            }

            return _scoreByTeamId.TryGetValue(
                teamId,
                out score);
        }

        /// <summary>
        /// TeamId の合計スコアを取得します。
        /// 未登録の場合は 0 を返します。
        /// </summary>
        /// <param name="teamId">取得対象の TeamId です。</param>
        /// <returns>合計スコアです。</returns>
        public int GetTeamScoreOrDefault(TeamId teamId)
        {
            if (teamId == TeamId.None)
            {
                return 0;
            }

            return _scoreByTeamId.TryGetValue(
                teamId,
                out int score)
                ? score
                : 0;
        }

        /// <summary>
        /// すべての登録情報と購読を解除します。
        /// </summary>
        public void Clear()
        {
            foreach (IDisposable killedSubscription
                     in _killedSubscriptionByStatus.Values)
            {
                killedSubscription?.Dispose();
            }

            _killedSubscriptionByStatus.Clear();
            _scoreByStatus.Clear();
            _scoreByTeamId.Clear();
            _characterIndexByStatus.Clear();
        }

        private bool HasSpawnedCharacter()
        {
            return _battleApplication != null
                   && _battleApplication.CharacterCount > 0;
        }

        private void RegisterCharactersFromBattleApplication()
        {
            _characterIndexByStatus.Clear();

            for (int i = 0;
                 i < _battleApplication.CharacterCount;
                 i++)
            {
                if (!_battleApplication.TryGetCharacterStatusAt(
                        i,
                        out CharacterStatus status))
                {
                    continue;
                }

                _characterIndexByStatus[status] = i;
                RegisterCharacter(status);
            }
        }

        private void HandleCharacterKilled(
            CharacterStatus killedStatus,
            CharacterStatus killerStatus)
        {
            if (killedStatus == null
                || killerStatus == null)
            {
                return;
            }

            if (killedStatus == killerStatus)
            {
                return;
            }

            if (killedStatus.TeamId == killerStatus.TeamId)
            {
                return;
            }

            EnsureCharacterRegistered(killerStatus);

            TransferScore(
                killedStatus,
                killerStatus);
        }

        private void EnsureCharacterRegistered(CharacterStatus status)
        {
            if (status == null)
            {
                return;
            }

            if (_scoreByStatus.ContainsKey(status))
            {
                return;
            }

            RegisterCharacter(status);
        }

        private void TransferScore(
            CharacterStatus sourceStatus,
            CharacterStatus destinationStatus)
        {
            int destinationScore =
                GetScoreOrDefault(destinationStatus);

            // 倒した側は常に +1 する（死んだ側のスコアはリセットしない）
            SetScore(
                destinationStatus,
                destinationScore + 1);
        }

        private void SetScore(
            CharacterStatus status,
            int score)
        {
            if (status == null)
            {
                return;
            }

            int validScore =
                Mathf.Max(0, score);

            if (_scoreByStatus.TryGetValue(
                    status,
                    out int currentScore)
                && currentScore == validScore)
            {
                return;
            }

            int previousScore =
                GetScoreOrDefault(status);

            _scoreByStatus[status] = validScore;

            ApplyTeamScoreDelta(
                status.TeamId,
                validScore - previousScore);

            PublishScoreChanged(status);
            WriteCharacterScoreToEntry(status);
            WriteModeResultToEntry();
        }

        private int GetInitialScore()
        {
            return Mathf.Max(0, _initialScore);
        }

        private void ApplyTeamScoreDelta(
            TeamId teamId,
            int delta)
        {
            if (teamId == TeamId.None)
            {
                return;
            }

            if (delta == 0)
            {
                return;
            }

            int currentScore =
                GetTeamScoreOrDefault(teamId);

            int nextScore =
                Mathf.Max(
                    0,
                    currentScore + delta);

            if (currentScore == nextScore)
            {
                return;
            }

            _scoreByTeamId[teamId] = nextScore;

            PublishTeamScoreChanged(teamId);
            WriteModeResultToEntry();
        }

        private void PublishScoreChanged(CharacterStatus status)
        {
            if (status == null)
            {
                return;
            }

            _scoreChangedSubject.OnNext(status);
        }

        private void PublishTeamScoreChanged(TeamId teamId)
        {
            if (teamId == TeamId.None)
            {
                return;
            }

            _teamScoreChangedSubject.OnNext(teamId);
        }

        private void WriteCharacterScoreToEntry(CharacterStatus status)
        {
            if (status == null)
            {
                return;
            }

            if (!_characterIndexByStatus.TryGetValue(
                    status,
                    out int characterIndex))
            {
                return;
            }

            _resultSceneEntry.SetWantedScore(
                characterIndex,
                GetScoreOrDefault(status));
        }

        private void WriteModeResultToEntry()
        {
            int primaryScore = GetTeamScoreOrDefault(TeamId.Primary);
            int secondaryScore = GetTeamScoreOrDefault(TeamId.Secondary);

            _resultSceneEntry.SetTeamScore(
                TeamId.Primary,
                primaryScore);

            _resultSceneEntry.SetTeamScore(
                TeamId.Secondary,
                secondaryScore);

            _resultSceneEntry.SetWinnerTeamId(
                ResolveWinnerTeamId(
                    primaryScore,
                    secondaryScore));
        }

        private static TeamId ResolveWinnerTeamId(
            int primaryScore,
            int secondaryScore)
        {
            if (primaryScore == secondaryScore)
            {
                return TeamId.None;
            }

            return primaryScore > secondaryScore
                ? TeamId.Primary
                : TeamId.Secondary;
        }

        private void OnDestroy()
        {
            Clear();

            _scoreChangedSubject.Dispose();
            _teamScoreChangedSubject.Dispose();
        }
    }
}
