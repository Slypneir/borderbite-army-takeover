namespace Core.BattleSystem.Enums
{
    /// <summary>What a <see cref="Structs.BattleEvent"/> tells the drawing and the sounds.</summary>
    public enum EventKind
    {
        Pickup,
        Shot,
        Strike,
        SoldierDied,
        HeroDied,
        HeroRespawned,
        Built,
        Destroyed,
        Hired,
        /// <summary>A barracks (Id: the place) or a base (Id: -1) of the side was hit; Value is the health left.</summary>
        BuildingHit,
        /// <summary>The order of the army of the side changed; Value is the new <see cref="ArmyOrder"/>, Id 1 when it changed by
        /// itself (no targets left, T18), 0 by a command.</summary>
        OrderChanged,
        /// <summary>
        /// A blow at a unit of the player on its own land did not go through: its shield (task 4.32). Id: the soldier's id, or
        /// −1 − the index of a hero.
        /// </summary>
        Shielded,
        /// <summary>The second chance went to the side (task 4.36); Value is the side that held it, Neutral at the first.</summary>
        SecondChance,
    }
}
