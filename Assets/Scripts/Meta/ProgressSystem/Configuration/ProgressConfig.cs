using Meta.ProgressSystem.Enums;
using UnityEngine;

namespace Meta.ProgressSystem.Configuration
{
    /// <summary>Crowns for a level and the lasting upgrades (GDD 6, stage 1 spec 5.2). All are starting values.</summary>
    [CreateAssetMenu(fileName = "ProgressConfig", menuName = "Borderbite/Progress/Progress Config")]
    public sealed class ProgressConfig : ScriptableObject
    {
        [Header("Crowns")]
        [Tooltip("A victory gives this many crowns… Starting value: 10.")]
        [Min(0)] [SerializeField] private int victoryCrowns = 10;

        [Tooltip("…and this many more for each level number. Starting value: 2.")]
        [Min(0)] [SerializeField] private int crownsPerLevel = 2;

        [Tooltip("A defeat gives this many crowns: progress even when stuck. Starting value: 3.")]
        [Min(0)] [SerializeField] private int defeatCrowns = 3;

        [Header("Upgrades")]
        [Tooltip("Steps of each upgrade. Starting value: 10.")]
        [Min(1)] [SerializeField] private int steps = 10;

        [Tooltip("The price of step 1, crowns. Starting value: 20.")]
        [Min(1)] [SerializeField] private float firstPrice = 20f;

        [Tooltip("Each next step costs this many times more, rounded. Starting value: 1.5.")]
        [Min(1f)] [SerializeField] private float priceGrowth = 1.5f;

        [Tooltip("Health of the hero per step, points; a blow stays 1 (T14). Starting value: 1.")]
        [Min(0)] [SerializeField] private int heroPowerStep = 1;

        [Tooltip("Brush of the hero per step, cells of radius. Starting value: 0.25.")]
        [Min(0f)] [SerializeField] private float heroBrushStep = 0.25f;

        [Tooltip("Health of each soldier per step, points; a blow stays 1 (T14). Starting value: 1.")]
        [Min(0)] [SerializeField] private int armyPowerStep = 1;

        [Tooltip("Start gold per step. Starting value: 1 (a soldier costs 1, T22).")]
        [Min(0)] [SerializeField] private int startGoldStep = 1;

        public int Steps => steps;
        public int HeroPowerStep => heroPowerStep;
        public float HeroBrushStep => heroBrushStep;
        public int ArmyPowerStep => armyPowerStep;
        public int StartGoldStep => startGoldStep;

        public int CrownsFor(bool victory, int level) => victory ? victoryCrowns + crownsPerLevel * level : defeatCrowns;

        /// <summary>The price of a step 1..<see cref="Steps"/>: 20 × 1.5^(step − 1), rounded.</summary>
        public int PriceOf(int step) => Mathf.RoundToInt(firstPrice * Mathf.Pow(priceGrowth, step - 1));

        /// <summary>What one step gives, for the card: points of health, cells or gold.</summary>
        public float StepOf(Upgrade upgrade) => upgrade switch
        {
            Upgrade.HeroPower => heroPowerStep,
            Upgrade.HeroBrush => heroBrushStep,
            Upgrade.ArmyPower => armyPowerStep,
            _ => startGoldStep,
        };
    }
}
