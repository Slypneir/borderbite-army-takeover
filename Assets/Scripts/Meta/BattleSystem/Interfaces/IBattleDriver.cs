using Core.BattleSystem.Services;

namespace Meta.BattleSystem.Interfaces
{
    /// <summary>
    /// Runs a battle on the frames of the engine, in real time and only while the game is not frozen.
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
        BattleRunner StartBattle(uint seed);

        /// <summary>
        /// Stops the battle
        /// </summary>
        void StopBattle();
    }
}
