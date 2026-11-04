namespace Core.BattleSystem.Enums
{
    /// <summary>What a soldier walks to (owner, 4 November; T15).</summary>
    public enum SoldierGoal
    {
        None,
        Hero,
        Soldier,
        Barracks,
        Base,
        /// <summary>A cell to paint (the order «Захват»); the goal id is its index.</summary>
        Cell,
    }
}
