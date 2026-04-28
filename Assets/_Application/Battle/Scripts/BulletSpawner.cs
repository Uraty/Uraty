using UnityEngine;
using Uraty.Shared.Battle;
using Uraty.Features.Player;

namespace Uraty.Application.Battle
{
    /// <summary>
    /// 実際の弾生成処理（Application側）
    /// </summary>
    public sealed class BulletSpawner : MonoBehaviour, IBulletSpawner
    {
        public void SpawnLineBullet(
            GameObject bulletPrefab,
            Transform ownerTransform,
            TeamId ownerTeamId,
            Vector3 startPosition,
            Vector3 direction,
            float maxTravelDistanceMeters,
            object definition,
            object attackDefinition)
        {
            PlayerBulletFactory.SpawnLineBullet(
                bulletPrefab,
                ownerTransform,
                ownerTeamId,
                startPosition,
                direction,
                maxTravelDistanceMeters,
                (LineAttackDefinition)definition,
                (AttackDefinition)attackDefinition);
        }

        public void SpawnFanBullet(
            GameObject bulletPrefab,
            Transform ownerTransform,
            TeamId ownerTeamId,
            Vector3 startPosition,
            Vector3 direction,
            float maxTravelDistanceMeters,
            object definition,
            object attackDefinition)
        {
            PlayerBulletFactory.SpawnFanBullet(
                bulletPrefab,
                ownerTransform,
                ownerTeamId,
                startPosition,
                direction,
                maxTravelDistanceMeters,
                (FanAttackDefinition)definition,
                (AttackDefinition)attackDefinition);
        }
    }
}
