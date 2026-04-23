using UnityEngine;

namespace Uraty.Shared.Battle
{
    public struct BulletHitContext
    {
        public Transform OwnerTransform;
        public TeamId OwnerTeamId;
        public Vector3 HitPoint;
        public Vector3 Direction;
        public bool CanBreakWalls;
        public bool CanBreakBushes;
        public bool IsRecovery;
    }
}
