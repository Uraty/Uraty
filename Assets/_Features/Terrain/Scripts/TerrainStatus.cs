using UnityEngine;
using TriInspector;
using Uraty.Shared.Battle;
using Uraty.Shared.Terrain;

namespace Uraty.Features.Terrain
{
    /// <summary>
    /// 地形の弾ヒット反応と Player 通行可否を管理する。
    /// </summary>
    public sealed class TerrainStatus : MonoBehaviour, IBulletHittable, IPlayerPassRule
    {
        [SerializeField] private TerrainKind _terrainKind;

        [SerializeField, LabelText("弾で破壊可能か")] private bool _canBeDestroyedByBullet;
        [SerializeField, LabelText("Wall に当たったときに弾を破壊するか")] private bool _destroyBulletOnWallHit = true;

        [Header("Player 通行設定")]
        [SerializeField, LabelText("Player 通行オーバーライドを使用するか")] private bool _usePlayerPassOverride = false;

        [Tooltip("これを true にすると、地形の種類に関係なく Player の通行可否がこの値で決まる。")]
        [SerializeField, LabelText("Player 通行可否のオーバーライド値")] private bool _canPlayerPassOverride = true;
        public bool CanPlayerPass(PlayerPassContext context)
        {
            if (_usePlayerPassOverride)
            {
                return _canPlayerPassOverride;
            }

            return _terrainKind switch
            {
                TerrainKind.Wall => false,
                TerrainKind.Fence => false,
                TerrainKind.Water => false,
                TerrainKind.Bush => true,
                TerrainKind.Generator => true,
                TerrainKind.Spawner => true,
                _ => true,
            };
        }

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
                // 地形を壊せた場合は、Terrain 側で弾消滅を確定しない。
                // 実際に弾を残すかは PlayerBullet 側の貫通設定で決める。
                return new BulletHitResponse
                {
                    WasHandled = true,
                    TerrainReaction = TerrainHitReaction.DestroyTerrain,
                    BulletReaction = BulletHitReaction.Pierce,
                    TargetKind = BulletHitTargetKind.Wall,
                };
            }

            return new BulletHitResponse
            {
                WasHandled = true,
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
                WasHandled = true,
                TerrainReaction = canDestroyTerrain
                    ? TerrainHitReaction.DestroyTerrain
                    : TerrainHitReaction.None,
                BulletReaction = BulletHitReaction.Pierce,
                TargetKind = BulletHitTargetKind.Bush,
            };
        }

        private BulletHitResponse ReceiveFloorHit()
        {
            return new BulletHitResponse
            {
                WasHandled = true,
                TerrainReaction = TerrainHitReaction.None,
                BulletReaction = BulletHitReaction.None,
                TargetKind = BulletHitTargetKind.None,
            };
        }
    }
}
