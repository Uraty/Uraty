using System.Collections.Generic;

using UnityEngine;

using Uraty.Feature.Akane_GameMode;
using Uraty.Shared.Role;
using Uraty.Shared.Team;
using Uraty.Application.Stage;

namespace Uraty.Application.Matching
{
    [CreateAssetMenu(fileName = "MatchingContext", menuName = "Game/MatchingContext")]
    public class MatchingContext : ScriptableObject
    {
        public const int BotCount = 5;
        public const int PrimaryBotCount = 2;
        public const int SecondaryBotCount = 3;

        //===============================
        //    ロビーシーンで登録するデータ群
        //===============================
        [Header("プレイヤー設定項目")]
        [SerializeField, Tooltip("マウス感度の倍率")]private float _mouseSensitivity = 1.0f;
        [SerializeField, Tooltip("スティック感度の倍率")]private float _stickSensitivityKey = 1.0f;
        [SerializeField, Tooltip("マウスのデッドゾーン")]private float _keyMouseDeadZone = 0.0f;
        [SerializeField, Tooltip("スティックのデッドゾーン")]private float _stickDeadZone = 0.2f;
        [SerializeField, Tooltip("SEのボリューム")]private float _seVolume = 1.0f;
        [SerializeField, Tooltip("BGMのボリューム")]private float _bgmVolume = 1.0f; 

        [Header("ゲームモードデータ")]
        [SerializeField] private GameModeData _gameModeData;

        [Header("ステージデータ")]
        [SerializeField] private StageData[] _stageData;

        [Header("プレイヤーデータ")]
        [SerializeField, Tooltip("プレイヤー本人のチームID")] private TeamId _playerTeamId;
        [SerializeField, Tooltip("プレイヤー本人の役職ID")] private RoleId _playerRoleId;

        //===============================
        //   マッチングシーンで登録するデータ群
        //===============================
        [Header("ボット５体の情報")]
        [SerializeField, Tooltip("ボットのチームID")] private TeamId[] _teamId = new TeamId[5];
        [SerializeField, Tooltip("ボットの役職")] private RoleId[] _roleId = new RoleId[5];

        private void Reset()
        {
            _mouseSensitivity = 1.0f;
            _stickSensitivityKey = 1.0f;
            _keyMouseDeadZone = 0.0f;
            _stickDeadZone = 0.2f;
            _seVolume = 1.0f;
            _bgmVolume = 1.0f;

            EnsureBotArraySize();
        }

        private void OnValidate()
        {
            EnsureBotArraySize();
        }

        public void SetMouseSensitivity(float value) => _mouseSensitivity = value;
        public void SetStickSensitivityKey(float value) => _stickSensitivityKey = value;
        public void SetKeyMouseDeadZone(float value) => _keyMouseDeadZone = value;
        public void SetStickDeadZone(float value) => _stickDeadZone = value;
        public void SetSeVolume(float value) => _seVolume = value;
        public void SetBgmVolume(float value) => _bgmVolume = value;
        public void SetGameModeData(GameModeData value) => _gameModeData = value;
        public void SetPlayerData(TeamId playerTeamId, RoleId playerRoleId)
        {
            _playerTeamId = playerTeamId;
            _playerRoleId = playerRoleId;
        }

        public void SetBotData(int index, TeamId teamId, RoleId roleId)
        {
            if (!IsValidBotIndex(index))
            {
                Debug.LogError($"{nameof(MatchingContext)}: Bot index が範囲外です。index={index}");
                return;
            }

            _teamId[index] = teamId;
            _roleId[index] = roleId;
        }
        public void ClearBotData()
        {
            EnsureBotArraySize();

            for (int i = 0; i < BotCount; i++)
            {
                _teamId[i] = default;
                _roleId[i] = default;
            }
        }
        public void SetTeamId(int index, TeamId value)
        {
            if (index >= 0 && index < _teamId.Length)
            {
                _teamId[index] = value;
            }
        }
        public void SetRoleId(int index, RoleId value)
        {
            if (index >= 0 && index < _roleId.Length)
            {
                _roleId[index] = value;
            }
        }

        public float MouseSensitivity => _mouseSensitivity;
        public float StickSensitivityKey => _stickSensitivityKey;
        public float KeyMouseDeadZone => _keyMouseDeadZone;
        public float StickDeadZone => _stickDeadZone;
        public float SeVolume => _seVolume;
        public float BgmVolume => _bgmVolume;
        public GameModeData GameModeData => _gameModeData;
        public TeamId PlayerTeamId => _playerTeamId;
        public RoleId PlayerRoleId => _playerRoleId;

        public IReadOnlyList<TeamId> TeamIds => _teamId;
        public IReadOnlyList<RoleId> RoleIds => _roleId;

        private bool IsValidBotIndex(int index)
        {
            return index >= 0 && index < BotCount;
        }

        private void EnsureBotArraySize()
        {
            if (_teamId == null || _teamId.Length != BotCount)
            {
                _teamId = new TeamId[BotCount];
            }

            if (_roleId == null || _roleId.Length != BotCount)
            {
                _roleId = new RoleId[BotCount];
            }
        }
    }
}
