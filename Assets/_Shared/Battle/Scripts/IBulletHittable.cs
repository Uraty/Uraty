namespace Uraty.Shared.Battle
{
    public interface IBulletHittable
    {
        BulletHitResponse ReceiveBulletHit(BulletHitContext context);
    }
}
