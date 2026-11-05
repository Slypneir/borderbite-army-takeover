using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A 3 × 3 place to build, given by its centre cell. A hero builds on it unless the cell is of the other side's colour (owner,
    /// 5 November: near the place is enough, as near a pile or an enemy); <see cref="Barracks"/> is the side whose barracks stands
    /// on it, or Neutral when it is empty.
    /// </summary>
    public struct SiteState
    {
        public int CenterX;
        public int CenterY;
        public Side Barracks;
        public int Health;
        public int BuildProgress;
        /// <summary>The side whose hero builds here now; the progress starts anew when another one takes over.</summary>
        public Side Builder;

        public bool Built => Barracks != Side.Neutral;
    }
}
