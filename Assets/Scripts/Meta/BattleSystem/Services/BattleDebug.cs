namespace Meta.BattleSystem.Services
{
    /// <summary>The overlay of the developer: the grid of cells, ticks and frames per second (stage 1 spec 2.7).</summary>
    public sealed class BattleDebug
    {
        public bool ShowDebug { get; set; }

        /// <summary>The camera shows the whole map from straight above, for screenshots of the map.</summary>
        public bool Overview { get; set; }
    }
}
