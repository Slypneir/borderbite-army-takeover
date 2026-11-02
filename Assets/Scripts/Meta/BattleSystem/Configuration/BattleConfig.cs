using Core.BattleSystem.Structs;
using UnityEngine;

namespace Meta.BattleSystem.Configuration
{
    /// <summary>The numbers of a battle. All are starting values: the game rules are still open questions (CLAUDE.md).</summary>
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Borderbite/Battle/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Tooltip("Simulation steps per second. Starting value: 30.")]
        [Min(1)] [SerializeField] private int ticksPerSecond = 30;

        [Tooltip("The most steps one frame may run after a hitch; the rest of the time is dropped. Starting value: 8.")]
        [Min(1)] [SerializeField] private int maxStepsPerFrame = 8;

        [Tooltip("Zones on the map, count. Starting value: 12.")]
        [Min(1)] [SerializeField] private int zoneCount = 12;

        [Tooltip("Factions in a battle, the player included, count. Starting value: 3.")]
        [Min(1)] [SerializeField] private int factionCount = 3;

        [Tooltip("Troops of a zone at the start, count. Starting value: 10.")]
        [Min(0)] [SerializeField] private int startTroops = 10;

        [Tooltip("Ticks between two new troops of a zone. Starting value: 30.")]
        [Min(1)] [SerializeField] private int productionPeriodTicks = 30;

        [Tooltip("Ticks a wave travels. Starting value: 60.")]
        [Min(1)] [SerializeField] private int waveTravelTicks = 60;

        [Tooltip("Random extra ticks of a wave, from 0 to this value. Starting value: 15.")]
        [Min(0)] [SerializeField] private int waveTravelJitterTicks = 15;

        public int MaxStepsPerFrame => maxStepsPerFrame;

        public BattleSettings ToSettings(uint seed) => new BattleSettings(ticksPerSecond, zoneCount, factionCount, startTroops,
            productionPeriodTicks, waveTravelTicks, waveTravelJitterTicks, seed);
    }
}
