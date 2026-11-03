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
    }
}
