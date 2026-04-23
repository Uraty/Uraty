using UnityEngine;

namespace Uraty.Shared.Battle
{
    /// <summary>
    /// 弾生成の共通インターフェース
    /// Feature からはこれだけを見る
    /// </summary>
    public interface IBulletSpawner
    {
        void SpawnLineBullet(
            GameObject bulletPrefab,
            Transform ownerTransform,
            TeamId ownerTeamId,
            Vector3 startPosition,
            Vector3 direction,
            float maxTravelDistanceMeters,
            object definition,
            object attackDefinition);

        void SpawnFanBullet(
            GameObject bulletPrefab,
            Transform ownerTransform,
            TeamId ownerTeamId,
            Vector3 startPosition,
            Vector3 direction,
            float maxTravelDistanceMeters,
            object definition,
            object attackDefinition);
    }
}
