using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A 3 × 3 place to build, given by its centre cell. It belongs to the side of the colour of that cell; <see cref="Barracks"/>
    /// is the side whose barracks stands on it, or Neutral when it is empty.
    /// </summary>
    public struct SiteState
    {
        public int CenterX;
        public int CenterY;
        public Side Barracks;
        public int Health;
        public int BuildProgress;

        public bool Built => Barracks != Side.Neutral;
    }
}
