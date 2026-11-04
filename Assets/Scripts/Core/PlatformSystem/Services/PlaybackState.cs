namespace Core.PlatformSystem.Services
{
    /// <summary>The reasons to stop the game and its sound; the two answers follow from them.</summary>
    public sealed class PlaybackState
    {
        public bool PlatformPaused;
        public bool PlatformMuted;
        public bool FocusLost;
        public bool AdRequested;
        public bool AdPlaying;
        /// <summary>The player's pause screen is open: it holds the game until the player goes on, whatever the focus does.</summary>
        public bool PlayerPaused;

        public bool Frozen => PlatformPaused || FocusLost || AdRequested || AdPlaying || PlayerPaused;

        public bool Muted => PlatformMuted || PlatformPaused || FocusLost || AdPlaying || PlayerPaused;
    }
}
