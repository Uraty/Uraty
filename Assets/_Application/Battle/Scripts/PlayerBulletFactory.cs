using UnityEngine;

using Uraty.Features.Player;
using Uraty.Shared.Battle;

namespace Uraty.Application.Battle
{
    public static class PlayerBulletFactory
    {
        public static PlayerBullet SpawnLineBullet(
            GameObject bulletPrefab,
            Transform ownerTransform,
            TeamId ownerTeamId,
            Vector3 startPosition,
            Vector3 direction,
            float maxTravelDistanceMeters,
            LineAttackDefinition definition,
            AttackDefinition attackDefinition)
        {
            GameObject bulletObject = Object.Instantiate(
                bulletPrefab,
                startPosition,
                Quaternion.LookRotation(direction.normalized));

            PlayerBullet playerBullet = GetOrAddComponent<PlayerBullet>(bulletObject);
            PlayerBulletMover playerBulletMover = GetOrAddComponent<PlayerBulletMover>(bulletObject);

            BulletRuntimeData runtimeData = CreateRuntimeData(
                ownerTransform,
                ownerTeamId,
                startPosition,
                direction,
                maxTravelDistanceMeters,
                definition.SpeedPerSecond,
                attackDefinition);

            playerBullet.Initialize(runtimeData);
            playerBulletMover.Initialize(runtimeData);
            return playerBullet;
        }

        public static PlayerBullet SpawnFanBullet(
            GameObject bulletPrefab,
            Transform ownerTransform,
            TeamId ownerTeamId,
            Vector3 startPosition,
            Vector3 direction,
            float maxTravelDistanceMeters,
            FanAttackDefinition definition,
            AttackDefinition attackDefinition)
        {
            GameObject bulletObject = Object.Instantiate(
                bulletPrefab,
                startPosition,
                Quaternion.LookRotation(direction.normalized));

            PlayerBullet playerBullet = GetOrAddComponent<PlayerBullet>(bulletObject);
            PlayerBulletMover playerBulletMover = GetOrAddComponent<PlayerBulletMover>(bulletObject);

            BulletRuntimeData runtimeData = CreateRuntimeData(
                ownerTransform,
                ownerTeamId,
                startPosition,
                direction,
                maxTravelDistanceMeters,
                definition.SpeedPerSecond,
                attackDefinition);

            playerBullet.Initialize(runtimeData);
            playerBulletMover.Initialize(runtimeData);
            return playerBullet;
        }

        private static BulletRuntimeData CreateRuntimeData(
            Transform ownerTransform,
            TeamId ownerTeamId,
            Vector3 startPosition,
            Vector3 direction,
            float maxTravelDistanceMeters,
            float speedMetersPerSecond,
            AttackDefinition attackDefinition)
        {
            return new BulletRuntimeData
            {
                OwnerTransform = ownerTransform,
                OwnerTeamId = ownerTeamId,
                StartPosition = startPosition,
                Direction = direction.normalized,
                SpeedMetersPerSecond = Mathf.Max(0f, speedMetersPerSecond),
                MaxTravelDistanceMeters = Mathf.Max(0f, maxTravelDistanceMeters),
                PenetrationSettings = attackDefinition.PenetrationSettings,
                CanBreakWalls = attackDefinition.CanBreakWalls,
                CanBreakBushes = attackDefinition.CanBreakBush,
                IsRecovery = attackDefinition.IsRecovery,
                CanMultiHit = attackDefinition.CanMultiHit,
            };
        }

        private static T GetOrAddComponent<T>(GameObject targetObject) where T : Component
        {
            T component = targetObject.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return targetObject.AddComponent<T>();
        }
    }
}
