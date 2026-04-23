using System.Collections.Generic;
using UnityEngine;
using Uraty.Shared.Battle;

namespace Uraty.Application.Battle
{
    public sealed class PlayerBullet : MonoBehaviour
    {
        private BulletRuntimeData _runtimeData;
        private bool _isInitialized;
        private readonly HashSet<int> _hitObjectInstanceIds = new();

        public void Initialize(BulletRuntimeData runtimeData)
        {
            _runtimeData = runtimeData;
            _isInitialized = true;
            _hitObjectInstanceIds.Clear();
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

        public bool CanHitObject(GameObject hitObject)
        {
            if (!_isInitialized || hitObject == null)
            {
                return false;
            }

            if (_runtimeData.CanMultiHit)
            {
                return true;
            }

            int instanceId = hitObject.GetInstanceID();
            if (_hitObjectInstanceIds.Contains(instanceId))
            {
                return false;
            }

            _hitObjectInstanceIds.Add(instanceId);
            return true;
        }

        public void ApplyHitResponse(BulletHitResponse response, GameObject hitObject)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("PlayerBullet is not initialized.", this);
                return;
            }

            if (response.TerrainReaction == TerrainHitReaction.DestroyTerrain && hitObject != null)
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
                        return;
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
                BulletHitTargetKind.Bush => _runtimeData.PenetrationSettings.CanPierceBush,
                _ => false,
            };
        }
    }
}
