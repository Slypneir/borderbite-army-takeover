namespace Core.PlatformSystem.Structs
{
    /// <summary>What the ad rules need to know about the session.</summary>
    public readonly struct AdContext
    {
        public readonly int BattlesFinished;
        public readonly double SecondsSinceInterstitial;
        public readonly bool RewardedOfferedSinceBattle;

        public AdContext(int battlesFinished, double secondsSinceInterstitial, bool rewardedOfferedSinceBattle)
        {
            BattlesFinished = battlesFinished;
            SecondsSinceInterstitial = secondsSinceInterstitial;
            RewardedOfferedSinceBattle = rewardedOfferedSinceBattle;
        }
    }
}
