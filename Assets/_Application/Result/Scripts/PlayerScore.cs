namespace Uraty.Application.Result
{
    public readonly struct PlayerScore
    {
        public string PlayerName
        {
            get;
        }
        public int KillCount
        {
            get;
        }
        public int DeathCount
        {
            get;
        }
        public int DamageDealt
        {
            get;
        }

        public PlayerScore(string playerName, int killCount, int deathCount, int damageDealt)
        {
            PlayerName = playerName;
            KillCount = killCount;
            DeathCount = deathCount;
            DamageDealt = damageDealt;
        }
    }
}
