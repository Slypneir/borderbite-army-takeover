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
            player.Health.Should().Be(200);
            enemy.X.Should().Be(56 * Unit);
            simulation.Heroes.Should().HaveCount(2);
            // task 4.24: both gates face the camera (south)
            simulation.Heroes[0].X.Should().Be(player.GateX).And.Be(player.X);
            simulation.Heroes[0].Y.Should().Be(player.GateY).And.BeLessThan(player.Y);
            simulation.Heroes[1].Side.Should().Be(Side.Enemy);
            simulation.Heroes[1].X.Should().Be(enemy.GateX).And.Be(enemy.X);
            simulation.Heroes[1].Y.Should().Be(enemy.GateY).And.BeLessThan(enemy.Y);
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
            // no enemy hero and no barracks: its soldiers go for the base; the player's hero walks away first, so no unit is in sight
            var settings = BattleDeterminismTests.Settings(layer3: true);
            settings.EnemyHero = false;
            var simulation = new BattleSimulation(settings);
            simulation.Enqueue(new BattleCommand(1, Side.Player, 1000, 1000));
            Run(simulation, 45);
            simulation.AddSoldiers(Side.Enemy, 5, 8 * Unit + 2500, 8 * Unit);

            Run(simulation, 120);

            simulation.Bases[(int)Side.Player].Health.Should().BeLessThan(200);
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
            slow.SetEnemyStrength(500, 33, 0);

            // a few ticks, while both still walk to their first goal
            Run(fast, 6);
            Run(slow, 6);

            var gate = fast.Bases[(int)Side.Enemy];
            var fastPath = Path(fast.Heroes[1], gate.GateX, gate.GateY);
            var slowPath = Path(slow.Heroes[1], gate.GateX, gate.GateY);
            slowPath.Should().BeLessThan(fastPath * 0.7);
        }

        [Test]
        public void Commander_CannotBuildBeforeItsSecond()
        {
            var simulation = New();
            simulation.SetEnemyStrength(800, 33, 60 * 30);
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

        [Test]
        public void Respawn_ThreeSecondsUnhurtable_NoEnemyPicksTheHero()
        {
            // task 4.23: back at home the hero takes no damage for 3 s, and enemy soldiers strike others meanwhile
            var settings = BattleDeterminismTests.Settings(layer3: true);
            settings.EnemyHero = false;
            var simulation = new BattleSimulation(settings);
            new BattleSettings().InvulnerableTicks.Should().Be(90);
            var hero = simulation.Heroes[0];
            simulation.AddSoldiers(Side.Enemy, 5, hero.X - 1500, hero.Y);
            for (var i = 0; i < 600 && !simulation.Events.Any(e => e.Kind == EventKind.HeroRespawned); i++)
                simulation.Step();
            var back = simulation.Events.First(e => e.Kind == EventKind.HeroRespawned).Tick;
            simulation.AddSoldiers(Side.Player, 1, simulation.Heroes[0].X + 1500, simulation.Heroes[0].Y);

            Run(simulation, 85);

            simulation.Heroes[0].Health.Should().Be(settings.HeroHealthOf(Side.Player));
            simulation.Events.Where(e => e.Kind == EventKind.Strike && e.Side == Side.Enemy && e.Tick > back && e.Tick < back + 85)
                .Should().OnlyContain(e => e.ToX != simulation.Heroes[0].X || e.ToY != simulation.Heroes[0].Y);
        }

        [Test]
        public void Hire_EnemyBarracks_SlowerThanThePlayer()
        {
            // task 4.23: the enemy hires every 0.8 s (24 ticks), the player every 0.5 s (15)
            var settings = BattleDeterminismTests.Settings(layer3: true);
            new BattleSettings().HireEveryOf(Side.Enemy).Should().Be(24);
            new BattleSettings().HireEveryOf(Side.Player).Should().Be(15);
            new BattleSettings().MineCooldownOf(Side.Enemy).Should().Be(12);
        }
    }
}
