using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A soldier: once hired it goes to attack on its own (owner, 4 November) — an enemy hero or soldier close by, else the nearest
    /// enemy barracks, else the enemy base. Health in points; <see cref="Id"/> stays the same for its whole life, for the drawing.
    /// </summary>
    public struct SoldierState
    {
        public int Id;
        public Side Side;
        public int X;
        public int Y;
        public int Health;
        public int Cooldown;
        /// <summary>The tick it last was hurt or struck; it heals only some time after it (T16).</summary>
        public int FightTick;
        /// <summary>What it walks to; kept until the target falls or an enemy comes close (T15).</summary>
        public SoldierGoal Goal;
        /// <summary>The index of a hero or a place, the id of a soldier; unused for the base.</summary>
        public int GoalId;
        public int FacingX;
        public int FacingY;
    }
}
