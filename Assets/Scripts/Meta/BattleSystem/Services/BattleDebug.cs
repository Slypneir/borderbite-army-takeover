using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;

namespace Meta.BattleSystem.Services
{
    /// <summary>
    /// The overlay of the developer (stage 1 spec 2.7): the grid of cells, ticks and frames per second, and the strength of the
    /// enemy, switched on the fly to that of any level of GDD 5.9 without starting the level anew; a new battle keeps it.
    /// </summary>
    public sealed class BattleDebug
    {
        private readonly BattleConfig _config;
        private readonly IBattleDriver _battle;

        public BattleDebug(BattleConfig config, IBattleDriver battle)
        {
            _config = config;
            _battle = battle;
            EnemyLevel = config.TestLevel;
            _battle.OnStarted += runner =>
            {
                if (EnemyLevel != _config.TestLevel)
                    _config.ApplyEnemy(runner.Simulation, EnemyLevel);
            };
        }

        public bool ShowDebug { get; set; }

        /// <summary>The developer froze the game for a screenshot: the pause screen stays shut.</summary>
        public bool QuietFreeze { get; set; }

        /// <summary>The camera shows the whole map from straight above, for screenshots of the map.</summary>
        public bool Overview { get; set; }

        /// <summary>The level whose enemy fights now.</summary>
        public int EnemyLevel { get; private set; }

        /// <summary>The map of the next battles (layer 5 spec 2.6); null — the map of the test level.</summary>
        public void PlayMap(Configuration.BattleMapAsset map) => _config.Play(map);

        public void SetEnemyLevel(int level)
        {
            EnemyLevel = UnityEngine.Mathf.Max(1, level);
            var simulation = _battle.Current?.Simulation;
            if (simulation != null)
                _config.ApplyEnemy(simulation, EnemyLevel);
        }
    }
}
