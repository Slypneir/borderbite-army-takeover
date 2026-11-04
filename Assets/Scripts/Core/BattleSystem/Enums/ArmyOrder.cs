namespace Core.BattleSystem.Enums
{
    /// <summary>
    /// An order to the whole army of a side (stage 1 spec 3.8, owner's decision of 4 November). <see cref="Free"/> — no order:
    /// the free attack of 3.4. The enemy gets no orders: its army follows its hero.
    /// </summary>
    public enum ArmyOrder
    {
        Free,
        /// <summary>«Ко мне»: a crowd 1–4 m around the hero, like the enemy's army.</summary>
        Rally,
        /// <summary>«Захват»: each to the nearest cell not of its side, neutral first, then the enemy's.</summary>
        Capture,
        /// <summary>«Бойцы»: the nearest enemy soldiers anywhere; strike soldiers and the hero, not buildings.</summary>
        Units,
        /// <summary>«Казармы»: the nearest enemy barracks; strike only barracks.</summary>
        Barracks,
        /// <summary>«Штаб»: the enemy base; strike only it.</summary>
        Base,
    }
}
