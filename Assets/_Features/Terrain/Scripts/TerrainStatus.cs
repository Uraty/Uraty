using UnityEngine;
using Uraty.Shared.Battle;

namespace Uraty.Feature.Terrain
{
    public sealed class TerrainStatus : MonoBehaviour, IBulletHittable
    {
        [SerializeField] private TerrainKind _terrainKind;
        [SerializeField] private bool _canBeDestroyedByBullet;
        [SerializeField] private bool _destroyBulletOnWallHit = true;

        public BulletHitResponse ReceiveBulletHit(BulletHitContext context)
        {
            return _terrainKind switch
            {
                TerrainKind.Wall => ReceiveWallHit(context),
                TerrainKind.Bush => ReceiveBushHit(context),
                _ => ReceiveFloorHit(),
            };
        }

        private BulletHitResponse ReceiveWallHit(BulletHitContext context)
        {
            bool canDestroyTerrain = _canBeDestroyedByBullet && context.CanBreakWalls;

            if (canDestroyTerrain)
            {
                return new BulletHitResponse
                {
                    TerrainReaction = TerrainHitReaction.DestroyTerrain,
                    BulletReaction = _destroyBulletOnWallHit
                        ? BulletHitReaction.DestroyBullet
                        : BulletHitReaction.Pierce,
                    TargetKind = BulletHitTargetKind.Wall,
                };
            }

            return new BulletHitResponse
            {
                TerrainReaction = TerrainHitReaction.None,
                BulletReaction = _destroyBulletOnWallHit
                    ? BulletHitReaction.DestroyBullet
                    : BulletHitReaction.Pierce,
                TargetKind = BulletHitTargetKind.Wall,
            };
        }

        private BulletHitResponse ReceiveBushHit(BulletHitContext context)
        {
            bool canDestroyTerrain = _canBeDestroyedByBullet && context.CanBreakBushes;

            return new BulletHitResponse
            {
                TerrainReaction = canDestroyTerrain
                    ? TerrainHitReaction.DestroyTerrain
                    : TerrainHitReaction.None,
                BulletReaction = BulletHitReaction.None,
                TargetKind = BulletHitTargetKind.None,
            };
        }

        private BulletHitResponse ReceiveFloorHit()
        {
            return new BulletHitResponse
            {
                TerrainReaction = TerrainHitReaction.None,
                BulletReaction = BulletHitReaction.None,
                TargetKind = BulletHitTargetKind.None,
            };
        }
    }
}
