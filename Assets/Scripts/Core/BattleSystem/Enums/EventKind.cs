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
    }
}
