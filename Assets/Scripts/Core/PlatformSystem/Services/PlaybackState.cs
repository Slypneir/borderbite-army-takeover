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

        public bool Frozen => PlatformPaused || FocusLost || AdRequested || AdPlaying;

        public bool Muted => PlatformMuted || PlatformPaused || FocusLost || AdPlaying;
    }
}
