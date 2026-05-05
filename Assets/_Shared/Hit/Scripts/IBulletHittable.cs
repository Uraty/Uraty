using UnityEngine;

using Uraty.Shared.Team;

namespace Uraty.Shared.Hit
{
    public interface IBulletHittable
    {
        bool ReceiveBulletHit(
            GameObject owner,
            TeamId teamId,
            float damage,
            bool isPiercing
            );
    }
}
