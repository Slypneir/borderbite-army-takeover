namespace Core.BattleSystem.Enums
{
    /// <summary>Who a cell or a hero belongs to; the value is stored in a byte per cell.</summary>
    public enum Side : byte
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2,
    }
}
