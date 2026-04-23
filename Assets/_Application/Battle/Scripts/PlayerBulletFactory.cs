using UnityEngine;

using Uraty.Feature.Player;
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

            PlayerBullet playerBullet = bulletObject.GetComponent<PlayerBullet>();
            if (playerBullet == null)
            {
                playerBullet = bulletObject.AddComponent<PlayerBullet>();
            }

            PlayerBulletMover playerBulletMover = bulletObject.GetComponent<PlayerBulletMover>();
            if (playerBulletMover == null)
            {
                playerBulletMover = bulletObject.AddComponent<PlayerBulletMover>();
            }

            BulletRuntimeData runtimeData = new BulletRuntimeData
            {
                OwnerTransform = ownerTransform,
                OwnerTeamId = ownerTeamId,
                StartPosition = startPosition,
                Direction = direction.normalized,
                SpeedMetersPerSecond = Mathf.Max(0f, definition.SpeedPerSecond),
                MaxTravelDistanceMeters = Mathf.Max(0f, maxTravelDistanceMeters),
                PenetrationSettings = definition.PenetrationSettings,
                CanBreakWalls = attackDefinition.CanBreakWalls,
                CanBreakBushes = attackDefinition.CanBreakGrass,
                IsRecovery = attackDefinition.IsRecovery,
            };

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

            PlayerBullet playerBullet = bulletObject.GetComponent<PlayerBullet>();
            if (playerBullet == null)
            {
                playerBullet = bulletObject.AddComponent<PlayerBullet>();
            }

            PlayerBulletMover playerBulletMover = bulletObject.GetComponent<PlayerBulletMover>();
            if (playerBulletMover == null)
            {
                playerBulletMover = bulletObject.AddComponent<PlayerBulletMover>();
            }

            BulletRuntimeData runtimeData = new BulletRuntimeData
            {
                OwnerTransform = ownerTransform,
                OwnerTeamId = ownerTeamId,
                StartPosition = startPosition,
                Direction = direction.normalized,
                SpeedMetersPerSecond = Mathf.Max(0f, definition.SpeedPerSecond),
                MaxTravelDistanceMeters = Mathf.Max(0f, maxTravelDistanceMeters),
                PenetrationSettings = definition.PenetrationSettings,
                CanBreakWalls = attackDefinition.CanBreakWalls,
                CanBreakBushes = attackDefinition.CanBreakGrass,
                IsRecovery = attackDefinition.IsRecovery,
            };

            playerBullet.Initialize(runtimeData);
            playerBulletMover.Initialize(runtimeData);
            return playerBullet;
        }
    }
}
