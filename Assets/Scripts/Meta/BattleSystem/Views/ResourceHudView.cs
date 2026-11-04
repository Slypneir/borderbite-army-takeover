using Core.BattleSystem.Enums;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using TMPro;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The counters of the HUD (stage 1 spec 3.6): gold, wood and the army «N/30». They show the «seen» numbers of
    /// <see cref="ResourceFlightView"/> (task 4.25): a mined unit counts when it lands, a spent one when it leaves, a hired
    /// soldier when its coin reaches it; the gap from the battle lasts at most a flight. Each change bumps its icon and number, and
    /// «+N» or «−N» gathers next to the number while changes come quicker than 0.8 s.
    /// </summary>
    public sealed class ResourceHudView : MonoBehaviour
    {
        private const float GainSeconds = 0.8f;
        private const float BumpSeconds = 0.24f;
        private static readonly Color GoldGain = new Color(1f, 0.827f, 0.302f, 1f);
        private static readonly Color WoodGain = new Color(0.941f, 0.753f, 0.541f, 1f);
        private static readonly Color Spent = new Color(0.96f, 0.95f, 0.88f, 0.6f);

        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text woodLabel;
        [SerializeField] private TMP_Text armyLabel;
        [SerializeField] private TMP_Text goldGain;
        [SerializeField] private TMP_Text woodGain;
        [SerializeField] private RectTransform goldIcon;
        [SerializeField] private RectTransform woodIcon;
        [SerializeField] private RectTransform armyIcon;

        private readonly Counter[] _counters = new Counter[3];
        private IBattleDriver _battle;
        private BattleConfig _rules;
        private int _gold = int.MinValue;
        private int _wood = int.MinValue;
        private int _army = int.MinValue;

        /// <summary>The seen numbers less the battle's: what is still in the air.</summary>
        public int GoldOffset { get; set; }
        public int WoodOffset { get; set; }
        public int ArmyOffset { get; set; }

        [Inject]
        public void Construct(IBattleDriver battle, BattleConfig rules)
        {
            _battle = battle;
            _rules = rules;
        }

        private void Awake()
        {
            _counters[0] = new Counter { Icon = goldIcon, Label = goldLabel, Gain = goldGain, Color = GoldGain };
            _counters[1] = new Counter { Icon = woodIcon, Label = woodLabel, Gain = woodGain, Color = WoodGain };
            _counters[2] = new Counter { Icon = armyIcon, Label = armyLabel };
            foreach (var counter in _counters)
                counter.Age = GainSeconds;
        }

        /// <summary>The icon of a counter (0 gold, 1 wood, 2 the army): where a flight lands or leaves.</summary>
        public RectTransform IconOf(int counter) => _counters[counter].Icon;

        /// <summary>A change of a counter: its icon and number bump up or sink; «+N» / «−N» gathers (none for the army).</summary>
        public void Change(int counter, int delta, bool quiet)
        {
            var c = _counters[counter];
            c.Bump = 0f;
            c.Up = delta > 0;
            if (quiet || c.Gain == null)
                return;

            c.Sum = c.Age < GainSeconds && Mathf.Sign(c.Sum) == Mathf.Sign(delta) ? c.Sum + delta : delta;
            c.Age = 0f;
            c.Gain.text = (c.Sum > 0 ? "+" : "–") + Mathf.Abs(c.Sum);
            c.Gain.color = c.Sum > 0 ? c.Color : Spent;
        }

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
                return;

            var purse = simulation.SideOf(Side.Player);
            var gold = Mathf.Max(0, purse.Gold + GoldOffset);
            var wood = Mathf.Max(0, purse.Wood + WoodOffset);
            var army = Mathf.Max(0, simulation.ArmyOf(Side.Player) + ArmyOffset);
            if (gold != _gold || wood != _wood || army != _army)
            {
                _gold = gold;
                _wood = wood;
                _army = army;
                goldLabel.text = gold.ToString();
                woodLabel.text = wood.ToString();
                armyLabel.text = army + "/" + _rules.ArmyMax;
            }

            foreach (var counter in _counters)
                Animate(counter);
        }

        private static void Animate(Counter c)
        {
            var dt = Time.unscaledDeltaTime;
            c.Bump += dt;
            var t = Mathf.Clamp01(c.Bump / BumpSeconds);
            var swing = Mathf.Sin(t * Mathf.PI) * (c.Up ? 0.3f : -0.16f);
            if (c.Icon != null)
                c.Icon.localScale = Vector3.one * (1f + swing);
            if (c.Label != null)
                c.Label.rectTransform.localScale = Vector3.one * (1f + swing * 0.5f);

            if (c.Gain == null)
                return;

            c.Age += dt;
            c.Gain.alpha = c.Age < GainSeconds ? c.Color.a * Mathf.Clamp01(1f - (c.Age - GainSeconds * 0.7f) / (GainSeconds * 0.3f)) : 0f;
            if (c.Sum < 0)
                c.Gain.alpha *= Spent.a;
        }

        private sealed class Counter
        {
            public RectTransform Icon;
            public TMP_Text Label;
            public TMP_Text Gain;
            public Color Color = Color.white;
            public float Bump = 1f;
            public bool Up;
            public int Sum;
            public float Age;
        }
    }
}
