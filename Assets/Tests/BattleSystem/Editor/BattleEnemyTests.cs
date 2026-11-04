using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>The rules of layer 3 of the stage 1 spec: bases 4.1, the enemy commander 4.2, what the signs of attack need 4.3.</summary>
    public sealed class BattleEnemyTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static BattleSimulation New() => new BattleSimulation(BattleDeterminismTests.Settings(layer3: true));

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks && simulation.Result == BattleResult.None; i++)
                simulation.Step();
        }

        [Test]
        public void Bases_InTheMiddleOfTheStartAreas_HeroesAtTheirGates()
        {
            var simulation = New();

            var player = simulation.Bases[(int)Side.Player];
            var enemy = simulation.Bases[(int)Side.Enemy];
            player.X.Should().Be(8 * Unit);
            player.Health.Should().Be(6000);
            enemy.X.Should().Be(56 * Unit);
            simulation.Heroes.Should().HaveCount(2);
            simulation.Heroes[0].X.Should().Be(player.GateX).And.BeGreaterThan(player.X);
            simulation.Heroes[1].Side.Should().Be(Side.Enemy);
            simulation.Heroes[1].X.Should().Be(enemy.GateX).And.BeLessThan(enemy.X);
        }

        [Test]
        public void Base_Aura_FourCellsProtected()
        {
            var simulation = New();

            simulation.Grid.IsProtected(8 * 64 + 8).Should().BeTrue();
            simulation.Grid.IsProtected(8 * 64 + 11).Should().BeTrue();
            simulation.Grid.IsProtected(8 * 64 + 13).Should().BeFalse();
        }

        [Test]
        public void EnemyBase_Falls_Victory()
        {
            var simulation = New();

            simulation.SetBaseHealth(Side.Enemy, 0);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Victory);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.Destroyed && e.Side == Side.Enemy && e.Id == -1);
        }

        [Test]
        public void PlayerBase_Falls_Defeat()
        {
            var simulation = New();

            simulation.SetBaseHealth(Side.Player, 0);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Defeat);
        }

        [Test]
        public void Base_SoldiersAtItsWalls_HitAndReported()
        {
            // no enemy hero: its soldiers keep to where they stand; the player's hero walks away first, so no unit is in sight
            var settings = BattleDeterminismTests.Settings(layer3: true);
            settings.EnemyHero = false;
            var simulation = new BattleSimulation(settings);
            simulation.Enqueue(new BattleCommand(1, Side.Player, 1000, 1000));
            Run(simulation, 45);
            simulation.AddSoldiers(Side.Enemy, 5, 8 * Unit + 2500, 8 * Unit);

            Run(simulation, 120);

            simulation.Bases[(int)Side.Player].Health.Should().BeLessThan(6000);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.BuildingHit && e.Side == Side.Player && e.Id == -1);
        }

        [Test]
        public void Commander_PlayerStandsAtHome_EnemyBuildsHiresAndComes()
        {
            var simulation = New();

            Run(simulation, 180 * 30);

            simulation.Sites.Should().Contain(s => s.Barracks == Side.Enemy);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.Hired && e.Side == Side.Enemy);
            // the enemy came to the player: its soldiers fought near the player's base, or the base fell
            var nearHome = simulation.Events.Any(e => e.Side == Side.Enemy && (e.Kind == EventKind.Strike || e.Kind == EventKind.Shot) &&
                                                      e.ToX < 24 * Unit && e.ToY < 24 * Unit);
            (nearHome || simulation.Result == BattleResult.Defeat).Should().BeTrue();
        }

        [Test]
        public void Commander_SlowerLevel_HeroWalksSlower()
        {
            var fast = New();
            var slow = New();
            slow.SetEnemyStrength(500, 33, 900, 0);

            // a few ticks, while both still walk to their first goal
            Run(fast, 6);
            Run(slow, 6);

            var start = fast.Bases[(int)Side.Enemy].GateX;
            var fastPath = Path(fast.Heroes[1], start, 56 * Unit);
            var slowPath = Path(slow.Heroes[1], start, 56 * Unit);
            slowPath.Should().BeLessThan(fastPath * 0.7);
        }

        [Test]
        public void Commander_CannotBuildBeforeItsSecond()
        {
            var simulation = New();
            simulation.SetEnemyStrength(800, 33, 900, 60 * 30);
            simulation.Give(Side.Enemy, 0, 40);

            Run(simulation, 50 * 30);

            simulation.Sites.Should().NotContain(s => s.Barracks == Side.Enemy);
        }

        [Test]
        public void Commander_SameSeed_SameBattle()
        {
            var one = New();
            var two = New();

            Run(one, 1500);
            Run(two, 1500);

            one.ComputeHash().Should().Be(two.ComputeHash());
        }

        [Test]
        public void Capture_OfTheOthersLand_CountsTheCellsLost()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings());
            simulation.Enqueue(new BattleCommand(1, Side.Player, 0, 0));
            simulation.PaintRect(Side.Enemy, 22, 22, 24, 24);
            simulation.PaintRect(Side.Player, 20, 20, 30, 20);
            simulation.PaintRect(Side.Player, 20, 30, 30, 30);
            simulation.PaintRect(Side.Player, 20, 20, 20, 30);
            simulation.PaintRect(Side.Player, 30, 20, 30, 30);

            simulation.Step();

            simulation.Captures.Should().ContainSingle().Which.FromEnemy.Should().Be(9);
        }

        private static double Path(HeroState hero, int x, int y) =>
            System.Math.Sqrt((double)(hero.X - x) * (hero.X - x) + (double)(hero.Y - y) * (hero.Y - y));
    }
}
