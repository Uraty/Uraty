namespace Uraty.Features.Character
{
    public readonly struct CharacterDamageEvent
    {
        public CharacterDamageEvent(
            CharacterStatus attackerStatus,
            CharacterStatus targetStatus,
            float damageAmount)
        {
            AttackerStatus = attackerStatus;
            TargetStatus = targetStatus;
            DamageAmount = damageAmount;
        }

        public CharacterStatus AttackerStatus
        {
            get;
        }

        public CharacterStatus TargetStatus
        {
            get;
        }

        public float DamageAmount
        {
            get;
        }
    }
}
