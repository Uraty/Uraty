using UnityEngine;

using Uraty.Shared.Hit;
using Uraty.Shared.Team;

public class StageWall : MonoBehaviour, IBulletHittable
{
    public bool ReceiveBulletHit(GameObject owner, TeamId teamId, float damage, bool isPiercing)
    {
        // 弾は壊す
        return true;
    }
}
