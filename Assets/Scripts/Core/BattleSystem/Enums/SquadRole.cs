namespace Core.BattleSystem.Enums
{
    /// <summary>What a squad of the enemy does (stage 1 spec 4.2, «Армия врага», task 4.27).</summary>
    public enum SquadRole
    {
        /// <summary>Fewer than the least of a squad yet: stays at home like <see cref="Home"/> and takes the new soldiers.</summary>
        Forming,
        /// <summary>Keeps by the base and the barracks.</summary>
        Guard,
        /// <summary>Between the waves: on its own land, paints its edge and the neutral land by it.</summary>
        Home,
        /// <summary>Chosen for the next wave: at home until it leaves; the sign of the wave is shown meanwhile.</summary>
        Gathering,
        /// <summary>Goes for the nearest barracks of the player, else its base.</summary>
        Wave,
        /// <summary>Goes back to its land: no target left, too few left, or the player's hero fell.</summary>
        Return,
    }
}
