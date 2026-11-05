using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>
    /// Task 4.27, spec 4.2 «Армия врага»: squads of 3–5 with one on guard, the rest at home off the player's land; a wave every
    /// so often with a warning first, to the nearest barracks of the player; the fall of the player's hero sends the waves home
    /// and spares the player's buildings while the hero is down; a truce at the start; the enemy's army at most the player's + 3.
    /// </summary>
    public sealed class BattleSquadTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static BattleSettings Squads()
        {
            var settings = BattleDeterminismTests.Settings(layer3: true);
            settings.EnemySquads = true;
            // a quick wave for the checks: every 2 s, the sign 1 s before
            settings.FirstWaveTicks = 60;
            settings.WaveEveryTicks = 60;
            settings.WaveWarnTicks = 30;
            settings.TruceTicks = 0;
            return settings;
        }

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        private static int Members(BattleSimulation simulation, int squad) => simulation.Soldiers.Count(s => s.Squad == squad);

        [Test]
        public void Squads_EightSoldiers_AGuardOfFiveAndASquadOfThreeAtHome()
        {
            var settings = Squads();
            settings.FirstWaveTicks = 100000;
            var simulation = new BattleSimulation(settings);
            simulation.AddSoldiers(Side.Enemy, 8, 52 * Unit, 50 * Unit);

            Run(simulation, 1);

            simulation.Squads.Should().HaveCount(2);
            simulation.Squads[0].Role.Should().Be(SquadRole.Guard);
            Members(simulation, simulation.Squads[0].Id).Should().Be(5);
            simulation.Squads[1].Role.Should().Be(SquadRole.Home);
            Members(simulation, simulation.Squads[1].Id).Should().Be(3);
        }

        [Test]
        public void Home_NeverStepsOnThePlayersLand()
        {
            var settings = Squads();
            settings.FirstWaveTicks = 100000;
            var simulation = new BattleSimulation(settings);
            simulation.PaintRect(Side.Player, 20, 20, 44, 44);
            simulation.AddSoldiers(Side.Enemy, 10, 46 * Unit, 46 * Unit);

            for (var i = 0; i < 300; i++)
            {
                simulation.Step();
                foreach (var soldier in simulation.Soldiers.Where(s => s.Side == Side.Enemy))
                    simulation.Grid.At(soldier.X / Unit, soldier.Y / Unit).Should().NotBe(Side.Player);
            }
        }

        [Test]
        public void Wave_SignFirst_ThenGoesForTheNearestBarracksOfThePlayer()
        {
            var simulation = new BattleSimulation(Squads());
            simulation.BuildBarracks(4, Side.Player);
            simulation.AddSoldiers(Side.Enemy, 13, 52 * Unit, 50 * Unit);

            Run(simulation, 31);
            simulation.WaveComing(out _, out _).Should().BeTrue();
            simulation.Squads.Should().Contain(s => s.Role == SquadRole.Gathering);

            Run(simulation, 30);

            simulation.WaveComing(out _, out _).Should().BeFalse();
            var waves = simulation.Squads.Where(s => s.Role == SquadRole.Wave).ToList();
            waves.Should().HaveCount(2);
            waves.Should().OnlyContain(s => s.Target == 4);
            simulation.Squads.Should().ContainSingle(s => s.Role == SquadRole.Guard);
        }

        [Test]
        public void PlayersHeroFalls_WavesGoHome_BuildingsSpared()
        {
            var simulation = new BattleSimulation(Squads());
            simulation.BuildBarracks(4, Side.Player);
            simulation.AddSoldiers(Side.Enemy, 13, 52 * Unit, 50 * Unit);
            Run(simulation, 62);
            simulation.Squads.Should().Contain(s => s.Role == SquadRole.Wave);

            simulation.HurtHero(Side.Player, 1000);
            Run(simulation, 1);

            simulation.Squads.Should().NotContain(s => s.Role == SquadRole.Wave);
            simulation.Squads.Should().Contain(s => s.Role == SquadRole.Return);

            // a soldier of the enemy at the walls of the player's barracks does not strike it while the hero is down
            var site = simulation.Sites[4];
            simulation.AddSoldiers(Side.Enemy, 1, BattleStateMiddle(site.CenterX) - 2900, BattleStateMiddle(site.CenterY));
            Run(simulation, 60);
            simulation.Events.Should().NotContain(e => e.Kind == EventKind.BuildingHit && e.Side == Side.Player && e.Id == 4);
        }

        [Test]
        public void Truce_NoEnemyOnThePlayersLandAtTheStart()
        {
            var settings = Squads();
            settings.TruceTicks = 150;
            var simulation = new BattleSimulation(settings);
            simulation.PaintRect(Side.Player, 30, 30, 40, 40);
            // the enemy's hero right by the player's land, walking into it by its commander or not: it stays off
            simulation.TeleportHero(Side.Enemy, 41 * Unit + 500, 35 * Unit);

            for (var i = 0; i < 140; i++)
            {
                simulation.Step();
                var hero = simulation.Heroes.First(h => h.Side == Side.Enemy);
                simulation.Grid.At(hero.X / Unit, hero.Y / Unit).Should().NotBe(Side.Player);
            }
        }

        [Test]
        public void ArmyCap_EnemyAtThePlayersArmyAndThree_StopsHiring()
        {
            var settings = Squads();
            settings.FirstWaveTicks = 100000;
            var simulation = new BattleSimulation(settings);
            simulation.BuildBarracks(1, Side.Enemy);
            simulation.Give(Side.Enemy, 100, 0);
            var site = simulation.Sites[1];
            simulation.TeleportHero(Side.Enemy, BattleStateMiddle(site.CenterX), BattleStateMiddle(site.CenterY) - 3500);

            Run(simulation, 600);

            // no army of the player: the floor of 5
            simulation.ArmyOf(Side.Enemy).Should().Be(5);
            // four of the player far away: the cap grows to 7, before they come to fight
            simulation.AddSoldiers(Side.Player, 4, 10 * Unit, 10 * Unit);
            Run(simulation, 120);
            simulation.ArmyOf(Side.Player).Should().Be(4);
            simulation.ArmyOf(Side.Enemy).Should().BeInRange(5, 7);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Alarm_AllAtHomeGoForTheIntruder_FarBeyond4m(bool alarm)
        {
            var settings = Squads();
            settings.FirstWaveTicks = 100000;
            settings.EnemyAlarm = alarm;
            settings.BrushDelayTicks = 12;
            var simulation = new BattleSimulation(settings);
            simulation.PaintRect(Side.Enemy, 0, 34, 63, 63);
            simulation.AddSoldiers(Side.Enemy, 8, 54 * Unit, 46 * Unit);
            Run(simulation, 1);
            // the player's hero paces to and fro on the enemy's land, 11 m and more from every soldier of the enemy: the brush
            // follows its trail, so the cell under it stays the enemy's while it walks (task 4.31)
            simulation.TeleportHero(Side.Player, 30 * Unit, 60 * Unit + Unit / 2);
            for (var leg = 0; leg < 16; leg++)
                simulation.Enqueue(new BattleCommand(simulation.Tick + 1 + leg * 6, Side.Player, leg % 2 == 0 ? 1000 : -1000, 0));

            Run(simulation, 90);

            var hero = simulation.Heroes.First(h => h.Side == Side.Player);
            var near = simulation.Soldiers.Count(s => s.Side == Side.Enemy &&
                                                      BattleStateDistance(s.X, s.Y, hero.X, hero.Y) < 4 * 2 * Unit);
            if (alarm)
                near.Should().BeGreaterOrEqualTo(4);
            else
                near.Should().Be(0);
        }

        private static long BattleStateDistance(int x0, int y0, int x1, int y1) =>
            (long)System.Math.Sqrt((double)(x1 - x0) * (x1 - x0) + (double)(y1 - y0) * (y1 - y0));

        private static int BattleStateMiddle(int cell) => cell * Unit + Unit / 2;
    }
}
