namespace Core.PlatformSystem.Enums
{
    /// <summary>Where in the game an advert is asked for. Each platform allows its own set (AdPolicyConfig).</summary>
    public enum AdPlacement
    {
        /// <summary>A battle is running: never, on any platform.</summary>
        ActiveBattle,

        /// <summary>The game itself moves between two different screens: after a battle, after a defeat.</summary>
        Transition,

        /// <summary>The player pressed «Next» or «Again» on the results.</summary>
        NextOrRetryButton,

        /// <summary>The player pressed a navigation button: menu, settings, shop.</summary>
        NavigationButton,
    }
}
