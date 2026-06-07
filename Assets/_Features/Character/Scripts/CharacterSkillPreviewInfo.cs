namespace Uraty.Features.Character
{
    public readonly struct CharacterSkillPreviewInfo
    {
        public CharacterSkillPreviewInfo(
            int bulletCount,
            float totalDamage,
            float maxRange,
            float maxSpeed)
        {
            BulletCount = bulletCount;
            TotalDamage = totalDamage;
            MaxRange = maxRange;
            MaxSpeed = maxSpeed;
        }

        public int BulletCount
        {
            get;
        }
        public float TotalDamage
        {
            get;
        }
        public float MaxRange
        {
            get;
        }
        public float MaxSpeed
        {
            get;
        }

        public bool IsValid => BulletCount > 0;
    }
}
