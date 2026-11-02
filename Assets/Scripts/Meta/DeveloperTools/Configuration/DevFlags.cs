namespace Meta.DeveloperTools.Configuration
{
    /// <summary>The developer flag: on in the editor and in development builds, a build for players has no way to turn it on.</summary>
    public static class DevFlags
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif
    }
}
