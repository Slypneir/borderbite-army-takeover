namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// What covers the battle now (UI review of layers 3–4): a window — results, upgrades or the pause — hides the marks over the
    /// world; between levels (results, upgrades) the HUD of the battle goes too. Set by the flow, read by the views.
    /// </summary>
    public sealed class BattleScreen
    {
        /// <summary>A window is over the battle: no labels, bars, popups, signs or hint over the world, no pause button.</summary>
        public bool Covered { get; set; }

        /// <summary>The results or the upgrades are shown: the HUD of the battle is hidden as well.</summary>
        public bool Between { get; set; }
    }
}
