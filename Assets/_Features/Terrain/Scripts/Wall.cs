using UnityEngine;

using Uraty.Shared.Team;
using Uraty.Shared.Hit;

namespace Uraty.Features.Terrain
{
    public class Wall : MonoBehaviour, IBulletHittable
    {
        public bool ReceiveBulletHit(GameObject owner, TeamId teamId, float damage, bool isPiercing)
        {
            return true;
        }
    }
}
