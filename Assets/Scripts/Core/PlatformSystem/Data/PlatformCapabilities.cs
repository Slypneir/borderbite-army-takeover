namespace Core.PlatformSystem.Data
{
    /// <summary>What differs between the platforms on the game side; everything else differs in the jslib of the platform.</summary>
    public sealed class PlatformCapabilities
    {
        /// <summary>A save may be written only after a successful read of the save.</summary>
        public bool LoadBeforeSave;

        /// <summary>The largest save, characters.</summary>
        public int SaveLimit;
    }
}
