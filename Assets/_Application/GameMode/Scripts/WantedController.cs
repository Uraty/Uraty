using System;

using UnityEngine;
using Uraty.Features.Gummy;
using Uraty.Features.Player;

namespace Uraty.Application.GameMode
{
    public sealed class WantedController : MonoBehaviour
    {
        // チーム数固定で2チーム制
        private const int TeamCount = 2;
        // チームIDの定義
        private const int Team0Id = 0;
        private const int Team1Id = 1;
        // 試合時間の最小値
        private const float MinMatchDurationSeconds = 0.0f;
        // 監視間隔の最小値
        // ０以下や異常値で毎フレーム重い重視を走らせないための下限
        private const float MinMonitorIntervalSeconds = 0.05f;
        // 勝利条件となるWantedScoreの最小値
        private const int MinTargetTeamWantedScore = 0;

        [Header("Scene References")]
        // Team0に所属するプレイヤー一覧
        [SerializeField] private PlayerStatus[] _team0Players = Array.Empty<PlayerStatus>();
        // Team1に所属するプレイヤー一覧
        [SerializeField] private PlayerStatus[] _team1Players = Array.Empty<PlayerStatus>();
        // シーン内のGummyを監視するための参照
        [SerializeField] private Uraty.Application.GummyCollection _gummyCollection;

        [Header("Rule")]
        // 試合時間
        [SerializeField] private float _matchDurationSeconds = 180.0f;
        // チームのWantedScoreがこの値に到達したら試合終了
        [SerializeField] private int _targetTeamWantedScore = 20;

        [Header("Monitor")]
        // プレイヤー状態やGummy状態を再監視する間隔
        [SerializeField] private float _monitorIntervalSeconds = 0.1f;

        // チームごとのWantedScore
        private readonly int[] _teamWantedScores = new int[TeamCount];
        // チームごとの現在所持スコア合計
        private readonly int[] _teamCarriedScores = new int[TeamCount];
        // チームごとの生存プレイヤー数
        private readonly int[] _teamAlivePlayerCounts = new int[TeamCount];
        // チームごとの死亡回数
        private readonly int[] _teamDeathCounts = new int[TeamCount];
        // 監視対象プレイヤーを１つの配列にまとめて扱うためのキャッシュ
        private MonitorPlayer[] _monitorPlayers = Array.Empty<MonitorPlayer>();
        // 残り試合時間
        private float _remainingMatchTimeSeconds;
        // 次に監視更新を行うまでのクールダウン
        private float _monitorCooldownSeconds;
        // 現在シーン内に存在する未拐取Gummy数
        private int _aliveGummyCount;
        // 回収完了扱いになったGummy数
        private int _completedGummyCount;
        // 試合終了フラグ
        private bool _isMatchFinished;

        // 外部参照用の読み取り専用プロパティ
        public float RemainingMatchTimeSeconds => _remainingMatchTimeSeconds;
        public bool IsMatchFinished => _isMatchFinished;
        public int Team0WantedScore => _teamWantedScores[Team0Id];
        public int Team1WantedScore => _teamWantedScores[Team1Id];
        public int Team0CarriedScore => _teamCarriedScores[Team0Id];
        public int Team1CarriedScore => _teamCarriedScores[Team1Id];
        public int Team0AlivePlayerCount => _teamAlivePlayerCounts[Team0Id];
        public int Team1AlivePlayerCount => _teamAlivePlayerCounts[Team1Id];
        public int Team0DeathCount => _teamDeathCounts[Team0Id];
        public int Team1DeathCount => _teamDeathCounts[Team1Id];
        public int AliveGummyCount => _aliveGummyCount;
        public int CompletedGummyCount => _completedGummyCount;
        public bool HasGummyCollection => _gummyCollection != null;

        private void Awake()
        {
            // Inspectorで不正な値が入っても最低限成立する値に補正する
            _matchDurationSeconds = Mathf.Max(MinMatchDurationSeconds, _matchDurationSeconds);
            _targetTeamWantedScore = Mathf.Max(MinTargetTeamWantedScore, _targetTeamWantedScore);
            _monitorIntervalSeconds = SanitizePositiveFiniteValue(
                _monitorIntervalSeconds,
                MinMonitorIntervalSeconds
            );

            // 試合開始時点の残り時間を設定
            _remainingMatchTimeSeconds = _matchDurationSeconds;
            // 開始直後にすぐ監視できるように0初期化
            _monitorCooldownSeconds = 0.0f;
        }

        private void Start()
        {
            // Team0 / 1 の配列から監視用プレイヤー一覧を構築
            BuildMonitorPlayers();
            // 開始時点のプレイヤー状態・Gummy状態を反映
            RefreshAllStates();
        }

        private void Update()
        {
            // 試合終了後は更新しない
            if (_isMatchFinished)
            {
                return;
            }
            // 残り時間更新
            UpdateMatchTimer();
            // 監視間隔に応じて状態再取得
            UpdateMonitorCooldown();
            // 勝利条件または時間切れの判定
            EvaluateMatchFinished();
        }

        public int GetTeamWantedScore(int teamId)
        {
            // 無効なteamIsの場合は安全に０を返す
            if (!IsValidTeamId(teamId))
            {
                return 0;
            }

            return _teamWantedScores[teamId];
        }

        public int GetTeamCarriedScore(int teamId)
        {
            if (!IsValidTeamId(teamId))
            {
                return 0;
            }

            return _teamCarriedScores[teamId];
        }

        public int GetTeamAlivePlayerCount(int teamId)
        {
            if (!IsValidTeamId(teamId))
            {
                return 0;
            }

            return _teamAlivePlayerCounts[teamId];
        }

        public int GetTeamDeathCount(int teamId)
        {
            if (!IsValidTeamId(teamId))
            {
                return 0;
            }

            return _teamDeathCounts[teamId];
        }

        private void BuildMonitorPlayers()
        {
            // Team0 / 1 の合計人数分だけ監視配列を確保
            int totalPlayerCount = _team0Players.Length + _team1Players.Length;
            _monitorPlayers = new MonitorPlayer[totalPlayerCount];
            // Team0 / 1 の順で監視配列に詰める
            int playerIndex = 0;
            playerIndex = AddMonitorPlayers(_team0Players, Team0Id, playerIndex);
            _ = AddMonitorPlayers(_team1Players, Team1Id, playerIndex);
        }

        private int AddMonitorPlayers(PlayerStatus[] players, int teamId, int startIndex)
        {
            int playerIndex = startIndex;

            for (int i = 0; i < players.Length; ++i)
            {
                PlayerStatus playerStatus = players[i];
                if (playerStatus == null)
                {
                    continue;
                }

                // 現在の所持スコアを取得し、監視開始時点の状態として記録する
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

        private void UpdateMatchTimer()
        {
            // 毎フレーム残り時間を減少
            _remainingMatchTimeSeconds -= Time.deltaTime;
            // 0未満にはしない
            if (_remainingMatchTimeSeconds < 0.0f)
            {
                _remainingMatchTimeSeconds = 0.0f;
            }
        }

        private void UpdateMonitorCooldown()
        {
            // 次回監視までの残り時間を減少
            _monitorCooldownSeconds -= Time.deltaTime;
            // まだ監視タイミングでなければ何もしない
            if (_monitorCooldownSeconds > 0.0f)
            {
                return;
            }
            // 次回監視タイミングを設定し、全状態を更新
            _monitorCooldownSeconds = _monitorIntervalSeconds;
            RefreshAllStates();
        }

        private void RefreshAllStates()
        {
            // チーム集計値をリセットしてから再集計する
            ResetTeamRuntimeValues();
            ObservePlayers();
            RefreshGummyStates();
        }

        private void ResetTeamRuntimeValues()
        {
            // 毎回再集計する値だけを初期化する
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

                // 現在の死亡状態と所持スコアを取得
                bool isDead = playerStatus.IsDead;
                int carriedScore = PeekCurrentScore(playerStatus);

                // 生存していれば所属チームの生存数を加算
                if (!isDead)
                {
                    _teamAlivePlayerCounts[monitorPlayer.TeamId]++;
                }

                // 所属チームの所持スコア合計を加算
                _teamCarriedScores[monitorPlayer.TeamId] += carriedScore;

                // 前回は生存、今回は死亡なら「撃破された」とみなす
                if (!monitorPlayer.WasDead && isDead)
                {
                    HandlePlayerDefeated(monitorPlayer.TeamId, monitorPlayer.CarriedScore);
                }

                // 次回比較用に最新状態を保存
                monitorPlayer.WasDead = isDead;
                monitorPlayer.CarriedScore = carriedScore;
                _monitorPlayers[i] = monitorPlayer;
            }
        }

        private void HandlePlayerDefeated(int defeatedTeamId, int defeatedCarriedScore)
        {
            if (!IsValidTeamId(defeatedTeamId))
            {
                return;
            }
            // 倒された側の相手チームを求める
            int opponentTeamId = GetOpponentTeamId(defeatedTeamId);
            if (!IsValidTeamId(opponentTeamId))
            {
                return;
            }

            // 倒されたチームの志望回数を増やし
            // 相手チームのWantedScoreに倒された側の所持スコアを加算する
            _teamDeathCounts[defeatedTeamId]++;
            _teamWantedScores[opponentTeamId] += Mathf.Max(0, defeatedCarriedScore);
        }

        private void RefreshGummyStates()
        {
            // シーン内のGummyStatusを取得して状態を再集計する
            GummyStatus[] gummyStatuses = FindObjectsByType<GummyStatus>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

            _aliveGummyCount = 0;
            _completedGummyCount = 0;

            for (int i = 0; i < gummyStatuses.Length; ++i)
            {
                GummyStatus gummyStatus = gummyStatuses[i];
                if (gummyStatus == null)
                {
                    continue;
                }
                // 回収完了済みならcompleted側へ
                if (gummyStatus.IsCollectionCompleted)
                {
                    _completedGummyCount++;
                    continue;
                }
                // まだ回収されていないものはalive扱い
                _aliveGummyCount++;
            }
        }

        private void EvaluateMatchFinished()
        {
            // 時間切れなら試合終了
            if (_remainingMatchTimeSeconds <= 0.0f)
            {
                _isMatchFinished = true;
                return;
            }
            // どちらかのチームが目標WantedScoreに到達しても試合終了
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

        private int PeekCurrentScore(PlayerStatus playerStatus)
        {
            if (playerStatus == null)
            {
                return 0;
            }
            // PlayerStatusに現在スコアの直接Getterがないため、
            // 一度TransferScoreで取得し、すぐ戻して現在地を覗き見る
            int transferredScore = playerStatus.TransferScore();
            if (transferredScore > 0)
            {
                playerStatus.ReceiveCollectedScore(transferredScore);
            }

            return transferredScore;
        }

        private int GetOpponentTeamId(int teamId)
        {
            if (teamId == Team0Id)
            {
                return Team1Id;
            }

            if (teamId == Team1Id)
            {
                return Team0Id;
            }

            return -1;
        }

        private bool IsValidTeamId(int teamId)
        {
            return teamId >= 0 && teamId < TeamCount;
        }

        private float SanitizePositiveFiniteValue(float value, float minValue)
        {
            // NaN / Infinityは危険なので最小値に置き換える
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return minValue;
            }

            return Mathf.Max(minValue, value);
        }

        private struct MonitorPlayer
        {
            // 監視対象のPlayerStatus
            public PlayerStatus PlayerStatus;
            // 所属チームID
            public int TeamId;
            // 前回監視次点で死亡していたか
            public bool WasDead;
            // 前回監視次点での所持スコア
            public int CarriedScore;

            public MonitorPlayer(
                PlayerStatus playerStatus,
                int teamId,
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
