namespace Uraty.Shared.Battle
{
    public readonly struct BulletHitResponse
    {
        public static BulletHitResponse None => new(false, false);
        public static BulletHitResponse Stop => new(true, false);
        public static BulletHitResponse PassThrough => new(true, true);

        public BulletHitResponse(bool wasHandled, bool canPassThrough)
        {
            WasHandled = wasHandled;
            CanPassThrough = canPassThrough;
        }

        public bool WasHandled
        {
            get;
        }
        public bool CanPassThrough
        {
            get;
        }
    }
}
