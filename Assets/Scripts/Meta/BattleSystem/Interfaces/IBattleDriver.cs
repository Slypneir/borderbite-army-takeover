using System;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;

namespace Meta.BattleSystem.Interfaces
{
    /// <summary>
    /// Runs a battle on the frames of the engine, in real time and only while the game is not frozen, and feeds it the direction
    /// of the player as commands.
    /// </summary>
    public interface IBattleDriver
    {
        /// <summary>
        /// The battle now; null when none runs
        /// </summary>
        BattleRunner Current { get; }

        /// <summary>
        /// Starts a new battle in place of the old one
        /// </summary>
        /// <summary>A new battle from the config; <paramref name="prepare"/> may change its settings first (the upgrades).</summary>
        BattleRunner StartBattle(uint seed, System.Action<Core.BattleSystem.Structs.BattleSettings> prepare = null);

        /// <summary>
        /// Stops the battle
        /// </summary>
        void StopBattle();

        /// <summary>
        /// A battle began: the views take the new one
        /// </summary>
        event Action<BattleRunner> OnStarted;

        /// <summary>
        /// The current battle ended with a victory or a defeat
        /// </summary>
        event Action<BattleResult> OnFinished;
    }
}
