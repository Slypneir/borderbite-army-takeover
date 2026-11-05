using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A soldier: the player's goes to attack on its own once hired (owner, 4 November) — an enemy hero or soldier close by, else
    /// the nearest enemy barracks, else the enemy base; the enemy's follows its hero (task 4.15). Health in points; <see cref="Id"/>
    /// stays the same for its whole life, for the drawing.
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
        /// <summary>Where it was put; a follower of a side without a hero keeps to it (the checks of the rules).</summary>
        public int AnchorX;
        public int AnchorY;
        /// <summary>The squad of a soldier of the enemy (task 4.27), 0 while it has none; the player's have none.</summary>
        public int Squad;
        /// <summary>The blows at it on its own land, for the shield of the player's side (task 4.32).</summary>
        public int ShieldHits;
        public int FacingX;
        public int FacingY;
    }
}
