using Core.BattleSystem.Enums;

namespace Core.BattleSystem.Structs
{
    /// <summary>
    /// A squad of 3–5 soldiers of the enemy (task 4.27): its id (the soldiers keep it in <see cref="SoldierState.Squad"/>), what it
    /// does, and the target of a wave — the index of a place with a barracks of the player, or -1 for the player's base.
    /// </summary>
    public struct SquadState
    {
        public int Id;
        public SquadRole Role;
        public int Target;
    }
}
