using UnityEngine;

namespace Uraty.Feature.Player
{
    public readonly struct PlayerBulletHitInfo
    {
        public PlayerBulletHitInfo(
            Collider hitCollider,
            Vector3 hitPoint,
            Vector3 hitNormal)
        {
            HitCollider = hitCollider;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
        }

        public Collider HitCollider
        {
            get;
        }

        public GameObject HitObject
        {
            get
            {
                return HitCollider != null ? HitCollider.gameObject : null;
            }
        }

        public Vector3 HitPoint
        {
            get;
        }

        public Vector3 HitNormal
        {
            get;
        }
    }
}
