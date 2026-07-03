using System;
using System.Collections;
using System.Reflection;

using R3;

using UnityEngine;
using UnityEngine.SceneManagement;

using Uraty.Features.Character;
using Uraty.Shared.Entry;
using Uraty.Shared.Role;
using Uraty.Shared.Team;

namespace Uraty.Application.Battle
{
    /// <summary>
    /// BattleApplication が生成した Character 情報を ResultSceneEntry に書き出し、
    /// CharacterStatus から通知される戦闘イベントを集計します。
    ///
    /// BattleApplication 本体に Mode 参照を持たせないため、同じ GameObject に自動追加されます。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class BattleResultEntryWriter : MonoBehaviour
    {
        private const float InstallRetryDelaySeconds = 0.1f;

        private static readonly BindingFlags PrivateInstanceFlags =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [SerializeField]
        private BattleApplication _battleApplication;

        [SerializeField]
        private ResultSceneEntry _resultSceneEntry;

        private readonly System.Collections.Generic.Dictionary<CharacterStatus, int>
            _characterIndexByStatus = new();

        private DisposableBag _disposables;
        private bool _isInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterFirstSceneLoad()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            TryInstallIntoActiveScene();
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            TryInstallIntoActiveScene();
        }

        private static void TryInstallIntoActiveScene()
        {
            BattleApplication[] applications =
                FindObjectsByType<BattleApplication>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            for (int i = 0; i < applications.Length; i++)
            {
                BattleApplication application = applications[i];

                if (application == null)
                {
                    continue;
                }

                if (application.TryGetComponent(out BattleResultEntryWriter _))
                {
                    continue;
                }

                BattleResultEntryWriter writer =
                    application.gameObject.AddComponent<BattleResultEntryWriter>();

                writer._battleApplication = application;
            }
        }

        private IEnumerator Start()
        {
            if (_battleApplication == null)
            {
                TryGetComponent(out _battleApplication);
            }

            while (_battleApplication == null)
            {
                yield return new WaitForSeconds(InstallRetryDelaySeconds);

                TryGetComponent(out _battleApplication);
            }

            yield return new WaitUntil(
                HasSpawnedCharacters);

            InitializeResultEntry();
        }

        private bool HasSpawnedCharacters()
        {
            return _battleApplication != null
                   && _battleApplication.CharacterCount > 0;
        }

        private void InitializeResultEntry()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            _resultSceneEntry.Clear();

            _characterIndexByStatus.Clear();

            TeamId playerTeamId = TeamId.None;

            for (int i = 0; i < _battleApplication.CharacterCount; i++)
            {
                if (!_battleApplication.TryGetCharacterStatusAt(
                        i,
                        out CharacterStatus status)
                    || status == null)
                {
                    continue;
                }

                bool isPlayer = i == BattleSceneEntry.PlayerIndex;

                if (isPlayer)
                {
                    playerTeamId = status.TeamId;
                }

                RoleType roleType = ResolveRoleType(status.gameObject);

                _resultSceneEntry.SetCharacterIdentity(
                    i,
                    status.TeamId,
                    roleType,
                    isPlayer);

                _characterIndexByStatus[status] = i;

                SubscribeCharacterStatus(status);
            }

            if (playerTeamId != TeamId.None
                && _resultSceneEntry.TryGetCharacter(
                    BattleSceneEntry.PlayerIndex,
                    out ResultCharacterEntry playerEntry))
            {
                _resultSceneEntry.SetCharacterIdentity(
                    BattleSceneEntry.PlayerIndex,
                    playerTeamId,
                    playerEntry.RoleType,
                    isPlayer: true);
            }
        }

        private void SubscribeCharacterStatus(CharacterStatus status)
        {
            status.DamageReceivedStream
                .Subscribe(HandleDamageReceived)
                .AddTo(ref _disposables);

            status.HealedStream
                .Subscribe(HandleHealed)
                .AddTo(ref _disposables);

            status.DiedStream
                .Subscribe(HandleDied)
                .AddTo(ref _disposables);

            status.KilledStream
                .Subscribe(killerStatus =>
                {
                    HandleKilled(
                        status,
                        killerStatus);
                })
                .AddTo(ref _disposables);
        }

        private void HandleDamageReceived(CharacterDamageEvent damageEvent)
        {
            float amount = Mathf.Max(0f, damageEvent.DamageAmount);

            if (amount <= 0f)
            {
                return;
            }

            if (damageEvent.TargetStatus != null
                && _characterIndexByStatus.TryGetValue(
                    damageEvent.TargetStatus,
                    out int targetIndex))
            {
                _resultSceneEntry.AddDamageTaken(
                    targetIndex,
                    amount);
            }

            if (damageEvent.AttackerStatus != null
                && _characterIndexByStatus.TryGetValue(
                    damageEvent.AttackerStatus,
                    out int attackerIndex))
            {
                _resultSceneEntry.AddDamageDealt(
                    attackerIndex,
                    amount);
            }
        }

        private void HandleHealed(CharacterHealEvent healEvent)
        {
            if (healEvent.TargetStatus == null)
            {
                return;
            }

            float amount = Mathf.Max(0f, healEvent.HealAmount);

            if (amount <= 0f)
            {
                return;
            }

            if (!_characterIndexByStatus.TryGetValue(
                    healEvent.TargetStatus,
                    out int characterIndex))
            {
                return;
            }

            _resultSceneEntry.AddHealingDone(
                characterIndex,
                amount);
        }

        private void HandleDied(CharacterStatus deadStatus)
        {
            if (deadStatus == null)
            {
                return;
            }

            if (!_characterIndexByStatus.TryGetValue(
                    deadStatus,
                    out int characterIndex))
            {
                return;
            }

            _resultSceneEntry.AddDeath(
                characterIndex);
        }

        private void HandleKilled(
            CharacterStatus killedStatus,
            CharacterStatus killerStatus)
        {
            if (killedStatus == null
                || killerStatus == null
                || killedStatus == killerStatus)
            {
                return;
            }

            if (!_characterIndexByStatus.TryGetValue(
                    killerStatus,
                    out int killerIndex))
            {
                return;
            }

            _resultSceneEntry.AddKill(
                killerIndex);
        }

        private RoleType ResolveRoleType(GameObject characterObject)
        {
            if (TryResolveRoleTypeByBattleApplicationPrefab(
                    characterObject,
                    out RoleType roleType))
            {
                return roleType;
            }

            return default;
        }

        private bool TryResolveRoleTypeByBattleApplicationPrefab(
            GameObject characterObject,
            out RoleType roleType)
        {
            roleType = default;

            if (characterObject == null
                || _battleApplication == null)
            {
                return false;
            }

            FieldInfo entriesField =
                typeof(BattleApplication).GetField(
                    "_roleCharacterPrefabEntries",
                    PrivateInstanceFlags);

            if (entriesField == null)
            {
                return false;
            }

            if (entriesField.GetValue(_battleApplication) is not Array entries)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                object entry = entries.GetValue(i);

                if (entry == null)
                {
                    continue;
                }

                if (!TryReadRolePrefabEntry(
                        entry,
                        out RoleType entryRoleType,
                        out GameObject prefab))
                {
                    continue;
                }

                if (prefab == null)
                {
                    continue;
                }

                if (!characterObject.name.StartsWith(
                        prefab.name,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                roleType = entryRoleType;
                return true;
            }

            return false;
        }

        private static bool TryReadRolePrefabEntry(
            object entry,
            out RoleType roleType,
            out GameObject prefab)
        {
            Type entryType = entry.GetType();

            PropertyInfo roleProperty =
                entryType.GetProperty(
                    "RoleType",
                    BindingFlags.Instance | BindingFlags.Public);

            PropertyInfo prefabProperty =
                entryType.GetProperty(
                    "CharacterPrefab",
                    BindingFlags.Instance | BindingFlags.Public);

            if (roleProperty != null
                && prefabProperty != null
                && roleProperty.GetValue(entry) is RoleType propertyRoleType)
            {
                roleType = propertyRoleType;
                prefab = prefabProperty.GetValue(entry) as GameObject;
                return true;
            }

            FieldInfo roleField =
                entryType.GetField(
                    "_roleType",
                    PrivateInstanceFlags);

            FieldInfo prefabField =
                entryType.GetField(
                    "_characterPrefab",
                    PrivateInstanceFlags);

            if (roleField == null
                || prefabField == null)
            {
                roleType = default;
                prefab = null;
                return false;
            }

            if (roleField.GetValue(entry) is not RoleType fieldRoleType)
            {
                roleType = default;
                prefab = null;
                return false;
            }

            roleType = fieldRoleType;
            prefab = prefabField.GetValue(entry) as GameObject;
            return true;
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
            _characterIndexByStatus.Clear();
        }
    }
}
