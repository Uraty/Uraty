using System;

using UnityEngine;

using Uraty.Feature.Gummy;
using Uraty.Feature.Player;
using Uraty.Shared.Battle;

namespace Uraty.Application.GameMode
{
    public sealed class WantedController : MonoBehaviour
    {
        // WantedController内部の集計配列で使うチーム数。
        private const int TeamCount = 2;

        // TeamIdの数値とは別に、配列アクセス専用のindexとして扱う。
        private const int Team0Index = 0;
        private const int Team1Index = 1;

        // 試合時間の最小値。
        private const float MinMatchDurationSeconds = 0.0f;

        // 0以下や異常値で重い監視処理を走らせ続けないための下限。
        private const float MinMonitorIntervalSeconds = 0.05f;

        // 勝利条件となるWantedScoreの最小値。
        private const int MinTargetTeamWantedScore = 0;

        // 撃破時に最低限加算するWantedScoreの最小値。
        private const int MinBaseDefeatWantedScore = 0;

        [Header("Scene References")]
        // 既存のInspector設定を壊さないため、Team0側の入力欄は残す。
        // 実際の所属チームはPlayerStatus.TeamIdを正として扱う。
        [SerializeField] private PlayerStatus[] _team0Players = Array.Empty<PlayerStatus>();

        // 既存のInspector設定を壊さないため、Team1側の入力欄は残す。
        // 実際の所属チームはPlayerStatus.TeamIdを正として扱う。
        [SerializeField] private PlayerStatus[] _team1Players = Array.Empty<PlayerStatus>();

        // 既存のInspector参照を壊さないために残す。
        // Gummyの状態集計自体はGummyStatusのキャッシュから行う。
        [SerializeField] private Uraty.Application.GummyCollection _gummyCollection;

        [Header("Rule")]
        // 試合時間。
        [SerializeField] private float _matchDurationSeconds = 180.0f;

        // チームのWantedScoreがこの値に到達したら試合終了。
        [SerializeField] private int _targetTeamWantedScore = 20;

        // 撃破時に、倒された相手の所持スコアとは別に加算する基本点。
        [SerializeField] private int _baseDefeatWantedScore = 1;

        [Header("Monitor")]
        // プレイヤー状態やGummy状態を再監視する間隔。
        [SerializeField] private float _monitorIntervalSeconds = 0.1f;

        // チームごとのWantedScore。
        private readonly int[] _teamWantedScores = new int[TeamCount];

        // チームごとの現在所持スコア合計。
        private readonly int[] _teamCarriedScores = new int[TeamCount];

        // チームごとの生存プレイヤー数。
        private readonly int[] _teamAlivePlayerCounts = new int[TeamCount];

        // チームごとの死亡回数。
        private readonly int[] _teamDeathCounts = new int[TeamCount];

        // 監視対象プレイヤーを1つの配列にまとめて扱うためのキャッシュ。
        private MonitorPlayer[] _monitorPlayers = Array.Empty<MonitorPlayer>();

        // 監視対象Gummyをキャッシュし、Update中のFindObjectsByType常用を避ける。
        private GummyStatus[] _gummyStatuses = Array.Empty<GummyStatus>();

        // 残り試合時間。
        private float _remainingMatchTimeSeconds;

        // 次に監視更新を行うまでのクールダウン。
        private float _monitorCooldownSeconds;

        // 現在シーン内に存在する未回収Gummy数。
        private int _aliveGummyCount;

        // 回収完了扱いになったGummy数。
        private int _completedGummyCount;

        // 試合終了フラグ。
        private bool _isMatchFinished;

        // 外部参照用の読み取り専用プロパティ。
        public float RemainingMatchTimeSeconds => _remainingMatchTimeSeconds;
        public bool IsMatchFinished => _isMatchFinished;
        public int Team0WantedScore => _teamWantedScores[Team0Index];
        public int Team1WantedScore => _teamWantedScores[Team1Index];
        public int Team0CarriedScore => _teamCarriedScores[Team0Index];
        public int Team1CarriedScore => _teamCarriedScores[Team1Index];
        public int Team0AlivePlayerCount => _teamAlivePlayerCounts[Team0Index];
        public int Team1AlivePlayerCount => _teamAlivePlayerCounts[Team1Index];
        public int Team0DeathCount => _teamDeathCounts[Team0Index];
        public int Team1DeathCount => _teamDeathCounts[Team1Index];
        public int AliveGummyCount => _aliveGummyCount;
        public int CompletedGummyCount => _completedGummyCount;
        public bool HasGummyCollection => _gummyCollection != null;

        private void Awake()
        {
            // Inspectorで不正な値が入っても最低限成立する値に補正する。
            _matchDurationSeconds = Mathf.Max(MinMatchDurationSeconds, _matchDurationSeconds);
            _targetTeamWantedScore = Mathf.Max(MinTargetTeamWantedScore, _targetTeamWantedScore);
            _baseDefeatWantedScore = Mathf.Max(MinBaseDefeatWantedScore, _baseDefeatWantedScore);
            _monitorIntervalSeconds = SanitizePositiveFiniteValue(
                _monitorIntervalSeconds,
                MinMonitorIntervalSeconds
            );

            // 試合開始時点の残り時間を設定する。
            _remainingMatchTimeSeconds = _matchDurationSeconds;

            // 開始直後にすぐ監視できるように0初期化する。
            _monitorCooldownSeconds = 0.0f;
        }

        private void Start()
        {
            // PlayerStatus.TeamIdを正として、監視用プレイヤー一覧を構築する。
            BuildMonitorPlayers();

            // Update中にFindObjectsByTypeを常用しないよう、開始時点でGummyをキャッシュする。
            RebuildGummyStatuses();

            // 開始時点のプレイヤー状態・Gummy状態を反映する。
            RefreshAllStates();
        }

        private void Update()
        {
            // 試合終了後は更新しない。
            if (_isMatchFinished)
            {
                return;
            }

            // 残り時間更新。
            UpdateMatchTimer();

            // 監視間隔に応じて状態再取得。
            UpdateMonitorCooldown();

            // 勝利条件または時間切れの判定。
            EvaluateMatchFinished();
        }

        public int GetTeamWantedScore(TeamId teamId)
        {
            int teamIndex = GetTeamIndex(teamId);
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamWantedScores[teamIndex];
        }

        public int GetTeamCarriedScore(TeamId teamId)
        {
            int teamIndex = GetTeamIndex(teamId);
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamCarriedScores[teamIndex];
        }

        public int GetTeamAlivePlayerCount(TeamId teamId)
        {
            int teamIndex = GetTeamIndex(teamId);
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamAlivePlayerCounts[teamIndex];
        }

        public int GetTeamDeathCount(TeamId teamId)
        {
            int teamIndex = GetTeamIndex(teamId);
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamDeathCounts[teamIndex];
        }

        public int GetTeamWantedScore(int teamIndex)
        {
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamWantedScores[teamIndex];
        }

        public int GetTeamCarriedScore(int teamIndex)
        {
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamCarriedScores[teamIndex];
        }

        public int GetTeamAlivePlayerCount(int teamIndex)
        {
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamAlivePlayerCounts[teamIndex];
        }

        public int GetTeamDeathCount(int teamIndex)
        {
            if (!IsValidTeamIndex(teamIndex))
            {
                return 0;
            }

            return _teamDeathCounts[teamIndex];
        }

        public void RebuildObservedObjects()
        {
            // 実行中にプレイヤーやGummyを生成し直した場合に、外部から監視対象を再構築できるようにする。
            BuildMonitorPlayers();
            RebuildGummyStatuses();
            RefreshAllStates();
        }

        private void BuildMonitorPlayers()
        {
            // 既存のTeam0 / Team1配列を入力元として使う。
            // ただし所属判定はPlayerStatus.TeamIdを正とする。
            int totalPlayerCount = _team0Players.Length + _team1Players.Length;
            _monitorPlayers = new MonitorPlayer[totalPlayerCount];

            int playerIndex = 0;
            playerIndex = AddMonitorPlayers(_team0Players, playerIndex);
            playerIndex = AddMonitorPlayers(_team1Players, playerIndex);

            // null、重複、TeamId.Noneを除外した実数に詰め直す。
            if (playerIndex < _monitorPlayers.Length)
            {
                Array.Resize(ref _monitorPlayers, playerIndex);
            }
        }

        private int AddMonitorPlayers(PlayerStatus[] players, int startIndex)
        {
            int playerIndex = startIndex;

            for (int i = 0; i < players.Length; ++i)
            {
                PlayerStatus playerStatus = players[i];
                if (playerStatus == null)
                {
                    continue;
                }

                if (HasRegisteredPlayer(playerStatus, playerIndex))
                {
                    continue;
                }

                TeamId teamId = playerStatus.TeamId;
                if (!IsPlayableTeamId(teamId))
                {
                    continue;
                }

                // 現在の所持スコアを取得し、監視開始時点の状態として記録する。
                int carriedScore = PeekCurrentScore(playerStatus);
                _monitorPlayers[playerIndex] = new MonitorPlayer(
                    playerStatus,
                    teamId,
                    playerStatus.IsDead,
                    carriedScore
                );

                playerIndex++;
            }

            return playerIndex;
        }

        private void RebuildGummyStatuses()
        {
            // Gummyの生成・破棄タイミングがある場合は、生成側からRebuildObservedObjectsを呼ぶ。
            _gummyStatuses = FindObjectsByType<GummyStatus>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
        }

        private void UpdateMatchTimer()
        {
            // 毎フレーム残り時間を減少させる。
            _remainingMatchTimeSeconds -= Time.deltaTime;

            // 0未満にはしない。
            if (_remainingMatchTimeSeconds < 0.0f)
            {
                _remainingMatchTimeSeconds = 0.0f;
            }
        }

        private void UpdateMonitorCooldown()
        {
            // 次回監視までの残り時間を減少させる。
            _monitorCooldownSeconds -= Time.deltaTime;

            // まだ監視タイミングでなければ何もしない。
            if (_monitorCooldownSeconds > 0.0f)
            {
                return;
            }

            // 次回監視タイミングを設定し、全状態を更新する。
            _monitorCooldownSeconds = _monitorIntervalSeconds;
            RefreshAllStates();
        }

        private void RefreshAllStates()
        {
            // チーム集計値をリセットしてから再集計する。
            ResetTeamRuntimeValues();
            ObservePlayers();
            RefreshGummyStates();
        }

        private void ResetTeamRuntimeValues()
        {
            // 毎回再集計する値だけを初期化する。
            for (int i = 0; i < TeamCount; ++i)
            {
                _teamCarriedScores[i] = 0;
                _teamAlivePlayerCounts[i] = 0;
            }
        }

        private void ObservePlayers()
        {
            for (int i = 0; i < _monitorPlayers.Length; ++i)
            {
                MonitorPlayer monitorPlayer = _monitorPlayers[i];
                PlayerStatus playerStatus = monitorPlayer.PlayerStatus;
                if (playerStatus == null)
                {
                    continue;
                }

                TeamId currentTeamId = playerStatus.TeamId;
                int teamIndex = GetTeamIndex(currentTeamId);
                if (!IsValidTeamIndex(teamIndex))
                {
                    continue;
                }

                // 現在の死亡状態と所持スコアを取得する。
                bool isDead = playerStatus.IsDead;
                int carriedScore = PeekCurrentScore(playerStatus);

                // 生存していれば所属チームの生存数と所持スコア合計を加算する。
                if (!isDead)
                {
                    _teamAlivePlayerCounts[teamIndex]++;
                    _teamCarriedScores[teamIndex] += carriedScore;
                }

                // 前回は生存、今回は死亡なら「撃破された」とみなす。
                if (!monitorPlayer.WasDead && isDead)
                {
                    HandlePlayerDefeated(currentTeamId, carriedScore);
                }

                // 次回比較用に最新状態を保存する。
                monitorPlayer.TeamId = currentTeamId;
                monitorPlayer.WasDead = isDead;
                monitorPlayer.CarriedScore = carriedScore;
                _monitorPlayers[i] = monitorPlayer;
            }
        }

        private void HandlePlayerDefeated(TeamId defeatedTeamId, int defeatedCarriedScore)
        {
            int defeatedTeamIndex = GetTeamIndex(defeatedTeamId);
            if (!IsValidTeamIndex(defeatedTeamIndex))
            {
                return;
            }

            // 倒された側の相手チームを求める。
            TeamId opponentTeamId = GetOpponentTeamId(defeatedTeamId);
            int opponentTeamIndex = GetTeamIndex(opponentTeamId);
            if (!IsValidTeamIndex(opponentTeamIndex))
            {
                return;
            }

            // 倒されたチームの死亡回数を増やし、相手チームのWantedScoreへ撃破点を加算する。
            _teamDeathCounts[defeatedTeamIndex]++;
            _teamWantedScores[opponentTeamIndex] += CalculateDefeatWantedScore(defeatedCarriedScore);
        }

        private void RefreshGummyStates()
        {
            _aliveGummyCount = 0;
            _completedGummyCount = 0;

            for (int i = 0; i < _gummyStatuses.Length; ++i)
            {
                GummyStatus gummyStatus = _gummyStatuses[i];
                if (gummyStatus == null)
                {
                    continue;
                }

                // 回収完了済みならcompleted側へ加算する。
                if (gummyStatus.IsCollectionCompleted)
                {
                    _completedGummyCount++;
                    continue;
                }

                // まだ回収されていないものはalive扱いにする。
                _aliveGummyCount++;
            }
        }

        private void EvaluateMatchFinished()
        {
            // 時間切れなら試合終了。
            if (_remainingMatchTimeSeconds <= 0.0f)
            {
                _isMatchFinished = true;
                return;
            }

            // どちらかのチームが目標WantedScoreに到達しても試合終了。
            for (int i = 0; i < TeamCount; ++i)
            {
                if (_teamWantedScores[i] < _targetTeamWantedScore)
                {
                    continue;
                }

                _isMatchFinished = true;
                return;
            }
        }

        private int CalculateDefeatWantedScore(int defeatedCarriedScore)
        {
            int safeCarriedScore = Mathf.Max(0, defeatedCarriedScore);
            return _baseDefeatWantedScore + safeCarriedScore;
        }

        private int PeekCurrentScore(PlayerStatus playerStatus)
        {
            if (playerStatus == null)
            {
                return 0;
            }

            // PlayerStatusに現在スコアの直接Getterがないため、
            // TransferScoreで一度取得し、ReceiveCollectedScoreですぐ戻して現在値を読む。
            int transferredScore = playerStatus.TransferScore();
            if (transferredScore > 0)
            {
                playerStatus.ReceiveCollectedScore(transferredScore);
            }

            return transferredScore;
        }

        private TeamId GetOpponentTeamId(TeamId teamId)
        {
            if (teamId == TeamId.Team0)
            {
                return TeamId.Team1;
            }

            if (teamId == TeamId.Team1)
            {
                return TeamId.Team0;
            }

            return TeamId.None;
        }

        private int GetTeamIndex(TeamId teamId)
        {
            if (teamId == TeamId.Team0)
            {
                return Team0Index;
            }

            if (teamId == TeamId.Team1)
            {
                return Team1Index;
            }

            return -1;
        }

        private bool IsPlayableTeamId(TeamId teamId)
        {
            return teamId == TeamId.Team0 || teamId == TeamId.Team1;
        }

        private bool IsValidTeamIndex(int teamIndex)
        {
            return teamIndex >= 0 && teamIndex < TeamCount;
        }

        private bool HasRegisteredPlayer(PlayerStatus playerStatus, int registeredPlayerCount)
        {
            for (int i = 0; i < registeredPlayerCount; ++i)
            {
                if (_monitorPlayers[i].PlayerStatus == playerStatus)
                {
                    return true;
                }
            }

            return false;
        }

        private float SanitizePositiveFiniteValue(float value, float minValue)
        {
            // NaN / Infinityは危険なので最小値に置き換える。
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minValue;
            }

            return Mathf.Max(minValue, value);
        }

        private struct MonitorPlayer
        {
            // 監視対象のPlayerStatus。
            public PlayerStatus PlayerStatus;

            // 所属チームID。
            public TeamId TeamId;

            // 前回監視時点で死亡していたか。
            public bool WasDead;

            // 前回監視時点での所持スコア。
            public int CarriedScore;

            public MonitorPlayer(
                PlayerStatus playerStatus,
                TeamId teamId,
                bool wasDead,
                int carriedScore)
            {
                PlayerStatus = playerStatus;
                TeamId = teamId;
                WasDead = wasDead;
                CarriedScore = carriedScore;
            }
        }
    }
}
