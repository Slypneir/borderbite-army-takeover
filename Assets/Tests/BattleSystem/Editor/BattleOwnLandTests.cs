using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>
    /// Task 4.32, spec 2.4a «своя земля помогает»: +15 % of speed on own land, −15 % on the other side's, as is on neutral land; the
    /// shield of the player's side on its own land — every 3rd blow at a unit does not go through, the enemy has none; healing
    /// at home twice as often. Every number is a setting of the battle.
    /// </summary>
    public sealed class BattleOwnLandTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static BattleSettings Home()
        {
            var settings = BattleDeterminismTests.Settings();
            settings.OwnLandSpeedPermille = 1150;
            settings.EnemyLandSpeedPermille = 850;
            settings.ShieldEvery = 3;
            settings.HomeHealPermille = 2000;
            // the brush on the trail: the cell under a walking hero keeps its colour (task 4.31)
            settings.BrushDelayTicks = 12;
            return settings;
        }

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        [TestCase(Side.Player, 1150)]
        [TestCase(Side.Enemy, 850)]
        [TestCase(Side.Neutral, 1000)]
        public void Speed_ByTheLandUnderTheHero(Side land, int permille)
        {
            var settings = Home();
            var simulation = new BattleSimulation(settings);
            if (land != Side.Neutral)
                simulation.PaintRect(land, 20, 20, 50, 40);
            simulation.TeleportHero(Side.Player, 30 * Unit + Unit / 2, 30 * Unit + Unit / 2);
            simulation.Enqueue(new BattleCommand(1, Side.Player, 1000, 0));
            var x = simulation.Heroes[0].X;

            Run(simulation, 1);

            (simulation.Heroes[0].X - x).Should().Be(settings.HeroStepUnits * permille / 1000);
        }

        [Test]
        public void Shield_OnOwnLand_EveryThirdBlowAtAUnitOfThePlayerStopped()
        {
            var simulation = new BattleSimulation(Home());
            simulation.PaintRect(Side.Player, 20, 20, 44, 44);
            simulation.AddSoldiers(Side.Player, 1, 32 * Unit, 32 * Unit);
            simulation.AddSoldiers(Side.Enemy, 1, 33 * Unit, 32 * Unit);
            var player = simulation.Soldiers.First(s => s.Side == Side.Player).Id;
            var enemy = simulation.Soldiers.First(s => s.Side == Side.Enemy).Id;

            Run(simulation, 150);

            var blows = simulation.Events.Count(e => e.Kind == EventKind.Strike && e.Side == Side.Enemy && e.Id == enemy);
            var shielded = simulation.Events.Count(e => e.Kind == EventKind.Shielded && e.Id == player);
            blows.Should().BeGreaterOrEqualTo(3);
            shielded.Should().Be(blows / 3);
            // the enemy's soldier, struck as often on the player's land, has no shield
            simulation.Events.Should().NotContain(e => e.Kind == EventKind.Shielded && e.Id == enemy);
        }

        [TestCase(2000, 3)]
        [TestCase(1000, 2)]
        public void Healing_AtHome_TwiceAsOften(int permille, int healed)
        {
            var settings = Home();
            settings.HomeHealPermille = permille;
            var simulation = new BattleSimulation(settings);
            Run(simulation, 1);
            // the hero at its base, on its own land: 4 points down; it heals 1 s later, then every 0.5 s — 0.25 s at home
            simulation.HurtHero(Side.Player, 4);
            var low = simulation.Heroes[0].Health;

            Run(simulation, settings.PlayerHeroHealAfterTicks + 2 * 8 + 1);

            (simulation.Heroes[0].Health - low).Should().Be(healed);
        }
    }
}
