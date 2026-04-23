using UnityEngine;
using Uraty.Shared.Battle;

namespace Uraty.Application.Battle
{
    public struct BulletRuntimeData
    {
        public Transform OwnerTransform;
        public TeamId OwnerTeamId;
        public Vector3 StartPosition;
        public Vector3 Direction;
        public float SpeedMetersPerSecond;
        public float MaxTravelDistanceMeters;
        public bool CanBreakWalls;
        public bool CanBreakBushes;
        public bool IsRecovery;
        public bool CanMultiHit;
        public BulletPenetrationSettings PenetrationSettings;
    }
}
