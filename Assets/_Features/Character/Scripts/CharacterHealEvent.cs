namespace Uraty.Features.Character
{
    public readonly struct CharacterHealEvent
    {
        public CharacterHealEvent(
            CharacterStatus targetStatus,
            float healAmount)
        {
            TargetStatus = targetStatus;
            HealAmount = healAmount;
        }

        public CharacterStatus TargetStatus
        {
            get;
        }

        public float HealAmount
        {
            get;
        }
    }
}
