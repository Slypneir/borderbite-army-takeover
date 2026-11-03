using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using Meta.BattleSystem.Configuration;
using Meta.BattleSystem.Interfaces;
using Meta.BattleSystem.Utils;
using TMPro;
using UnityEngine;
using VContainer;

namespace Meta.BattleSystem.Views
{
    /// <summary>
    /// The counters of layer 2 in the HUD (stage 1 spec 3.6): gold, wood and the army «N/30»; a pickup shows «+5» next to its
    /// counter for a moment.
    /// </summary>
    public sealed class ResourceHudView : MonoBehaviour
    {
        private const float GainSeconds = 0.8f;

        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text woodLabel;
        [SerializeField] private TMP_Text armyLabel;
        [SerializeField] private TMP_Text goldGain;
        [SerializeField] private TMP_Text woodGain;

        private IBattleDriver _battle;
        private BattleConfig _rules;
        private EventCursor _events;
        private int _gold = -1;
        private int _wood = -1;
        private int _army = -1;
        private float _goldAge = GainSeconds;
        private float _woodAge = GainSeconds;

        [Inject]
        public void Construct(IBattleDriver battle, BattleConfig rules)
        {
            _battle = battle;
            _rules = rules;
        }

        private void LateUpdate()
        {
            var simulation = _battle?.Current?.Simulation;
            if (simulation == null)
                return;

            _events.Read(simulation, Handle);
            var purse = simulation.SideOf(Side.Player);
            var army = simulation.ArmyOf(Side.Player);
            if (purse.Gold != _gold || purse.Wood != _wood || army != _army)
            {
                _gold = purse.Gold;
                _wood = purse.Wood;
                _army = army;
                goldLabel.text = _gold.ToString();
                woodLabel.text = _wood.ToString();
                armyLabel.text = _army + "/" + _rules.ArmyMax;
            }

            Fade(goldGain, ref _goldAge);
            Fade(woodGain, ref _woodAge);
        }

        private void Handle(BattleEvent battleEvent)
        {
            if (battleEvent.Kind != EventKind.Pickup || battleEvent.Side != Side.Player)
                return;

            var gold = battleEvent.Detail == (int)ResourceKind.Gold;
            var label = gold ? goldGain : woodGain;
            label.text = "+" + battleEvent.Value;
            if (gold)
                _goldAge = 0f;
            else
                _woodAge = 0f;
        }

        private static void Fade(TMP_Text label, ref float age)
        {
            age += Time.deltaTime;
            label.alpha = Mathf.Clamp01(1f - age / GainSeconds);
        }
    }
}
