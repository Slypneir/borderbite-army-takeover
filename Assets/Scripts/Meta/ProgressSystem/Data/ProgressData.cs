using System;

namespace Meta.ProgressSystem.Data
{
    /// <summary>
    /// What a player keeps between levels and visits (stage 1 spec 5.3), saved through the platform as JSON: the level, the
    /// crowns, the steps of the four upgrades and how far the hint of level 1 went (spec T11: once per player).
    /// </summary>
    [Serializable]
    public sealed class ProgressData
    {
        public int Version = 1;
        public int Level = 1;
        public int Crowns;
        public int[] Upgrades = new int[4];
        public int HintStep;
    }
}
