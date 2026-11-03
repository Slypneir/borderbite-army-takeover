using Core.BattleSystem.Structs;
using UnityEngine;

namespace Meta.BattleSystem.Configuration
{
    /// <summary>
    /// The rules of a battle in meters and seconds (stage 1 spec, layer 1). All are starting values; the simulation gets them in
    /// its own units through <see cref="ToSettings"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BattleConfig", menuName = "Borderbite/Battle/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Tooltip("Simulation steps per second. Starting value: 30.")]
        [Min(1)] [SerializeField] private int ticksPerSecond = 30;

        [Tooltip("The most steps one frame may run after a hitch; the rest of the time is dropped. Starting value: 8.")]
        [Min(1)] [SerializeField] private int maxStepsPerFrame = 8;

        [Tooltip("Cells along a side of the square map. Starting value: 64.")]
        [Min(8)] [SerializeField] private int mapCells = 64;

        [Tooltip("Side of a cell, meters. Starting value: 0.5.")]
        [Min(0.1f)] [SerializeField] private float cellSize = 0.5f;

        [Tooltip("Radius of a start area, cells. Starting value: 6.")]
        [Min(1)] [SerializeField] private int startRadius = 6;

        [Tooltip("Distance from the centre of a start area to the two nearest edges, cells. Starting value: 8.")]
        [Min(1)] [SerializeField] private int startOffset = 8;

        [Tooltip("Speed of the hero in any direction, meters per second. Starting value: 5.")]
        [Min(0.1f)] [SerializeField] private float heroSpeed = 5f;

        [Tooltip("Radius of the body of the hero: how close it comes to the edge of the map, meters. Starting value: 0.3.")]
        [Min(0f)] [SerializeField] private float heroRadius = 0.3f;

        [Tooltip("Radius of the brush of the hero: cells whose middle is this close become its colour, cells. Starting value: 2.")]
        [Min(0.5f)] [SerializeField] private float heroBrush = 2f;

        [Tooltip("The enclosure is looked for every this many ticks; 3 is the fallback for a slow device. Starting value: 1.")]
        [Min(1)] [SerializeField] private int enclosureEveryTicks = 1;

        [Tooltip("The largest area one enclosure takes, percent of the map; a larger one stays as it is. Starting value: 100 (no limit).")]
        [Range(1f, 100f)] [SerializeField] private float maxEnclosureShare = 100f;

        [Tooltip("A side with this share of the map wins, percent. Starting value: 70.")]
        [Range(1f, 100f)] [SerializeField] private float victoryShare = 70f;

        public int MaxStepsPerFrame => maxStepsPerFrame;
        public int TicksPerSecond => ticksPerSecond;
        public int MapCells => mapCells;
        public float CellSize => cellSize;
        public float MapSize => mapCells * cellSize;
        public float VictoryShare => victoryShare;

        /// <summary>Meters of the world for units of the simulation.</summary>
        public float MetersPerUnit => cellSize / BattleSettings.UnitsPerCell;

        public BattleSettings ToSettings(uint seed)
        {
            var unitsPerMeter = BattleSettings.UnitsPerCell / cellSize;
            return new BattleSettings(ticksPerSecond, mapCells, startRadius, startOffset,
                Mathf.RoundToInt(heroSpeed * unitsPerMeter / ticksPerSecond),
                Mathf.RoundToInt(heroRadius * unitsPerMeter),
                Mathf.RoundToInt(heroBrush * BattleSettings.UnitsPerCell),
                enclosureEveryTicks,
                Mathf.RoundToInt(maxEnclosureShare * 10f),
                Mathf.RoundToInt(victoryShare * 10f),
                seed);
        }
    }
}
