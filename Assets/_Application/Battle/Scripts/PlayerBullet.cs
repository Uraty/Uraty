using UnityEngine;
using Uraty.Shared.Battle;

namespace Uraty.Application.Battle
{
    public sealed class PlayerBullet : MonoBehaviour
    {
        private BulletRuntimeData _runtimeData;
        private bool _isInitialized;

        public void Initialize(BulletRuntimeData runtimeData)
        {
            _runtimeData = runtimeData;
            _isInitialized = true;
        }

        public BulletHitContext CreateHitContext(Vector3 hitPoint)
        {
            return new BulletHitContext
            {
                OwnerTransform = _runtimeData.OwnerTransform,
                OwnerTeamId = _runtimeData.OwnerTeamId,
                HitPoint = hitPoint,
                Direction = _runtimeData.Direction,
                CanBreakWalls = _runtimeData.CanBreakWalls,
                CanBreakBushes = _runtimeData.CanBreakBushes,
                IsRecovery = _runtimeData.IsRecovery,
            };
        }

        public void ApplyHitResponse(BulletHitResponse response, GameObject hitObject)
        {
            if (!_isInitialized)
            {
                return;
            }

            if (response.TerrainReaction == TerrainHitReaction.DestroyTerrain)
            {
                Destroy(hitObject);
            }

            switch (response.BulletReaction)
            {
                case BulletHitReaction.DestroyBullet:
                    Destroy(gameObject);
                    return;

                case BulletHitReaction.Pierce:
                    if (!CanPierceTarget(response.TargetKind))
                    {
                        Destroy(gameObject);
                    }
                    return;

                case BulletHitReaction.None:
                default:
                    return;
            }
        }

        private bool CanPierceTarget(BulletHitTargetKind targetKind)
        {
            return targetKind switch
            {
                BulletHitTargetKind.Player => _runtimeData.PenetrationSettings.CanPiercePlayer,
                BulletHitTargetKind.Wall => _runtimeData.PenetrationSettings.CanPierceWall,
                _ => false,
            };
        }
    }
}
