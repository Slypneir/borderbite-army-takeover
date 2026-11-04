using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>The orders of the army (stage 1 spec 3.8, T17–T20, task 4.17).</summary>
    public sealed class BattleOrderTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static BattleSimulation New()
        {
            var settings = BattleDeterminismTests.Settings();
            settings.BaseHealth = 100000;
            return new BattleSimulation(settings);
        }

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        private static void Order(BattleSimulation simulation, ArmyOrder order) =>
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, order)).Should().BeTrue();

        private static double Meters(int x0, int y0, int x1, int y1) =>
            System.Math.Sqrt((double)(x1 - x0) * (x1 - x0) + (double)(y1 - y0) * (y1 - y0)) / (2 * Unit);

        private static int Middle(int cell) => cell * Unit + Unit / 2;

        [Test]
        public void Order_None_AtStart_SameOrderAgainFrees()
        {
            var simulation = New();
            simulation.OrderOf(Side.Player).Should().Be(ArmyOrder.Free);

            Order(simulation, ArmyOrder.Base);
            Run(simulation, 1);
            simulation.OrderOf(Side.Player).Should().Be(ArmyOrder.Base);

            Order(simulation, ArmyOrder.Base);
            Run(simulation, 1);
            simulation.OrderOf(Side.Player).Should().Be(ArmyOrder.Free);
            simulation.Events.Count(e => e.Kind == EventKind.OrderChanged && e.Id == 0).Should().Be(2);
        }

        [Test]
        public void Order_Rally_ACrowdAroundTheHero()
        {
            var simulation = New();
            simulation.BuildBarracks(1, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 8, 20 * Unit, 20 * Unit);
            Order(simulation, ArmyOrder.Rally);

            Run(simulation, 90);

            var hero = simulation.Heroes[0];
            foreach (var soldier in simulation.Soldiers)
                Meters(soldier.X, soldier.Y, hero.X, hero.Y).Should().BeInRange(0.45, 4.3);
        }

        [Test]
        public void Order_Barracks_WalksPastSoldiersAndStrikesOnlyTheBarracks()
        {
            var simulation = New();
            simulation.BuildBarracks(2, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 3, 12 * Unit, 20 * Unit);
            // an enemy soldier right on the way, 1 m off: the free attack would turn on it
            simulation.AddSoldiers(Side.Enemy, 1, 14 * Unit, 24 * Unit);
            Order(simulation, ArmyOrder.Barracks);

            Run(simulation, 150);

            // every blow of a soldier lands on the barracks at (12, 36), none on the enemy soldier
            simulation.Events.Where(e => e.Kind == EventKind.Strike && e.Side == Side.Player && e.Id >= 0)
                .Should().NotBeEmpty().And.OnlyContain(e => e.ToX == Middle(12) && e.ToY == Middle(36));
            simulation.Events.Should().Contain(e => e.Kind == EventKind.BuildingHit && e.Side == Side.Enemy && e.Id == 2);
        }

        [Test]
        public void Order_Base_GoesToTheEnemyBase()
        {
            var simulation = New();
            simulation.BuildBarracks(2, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 1, 20 * Unit, 20 * Unit);
            var target = simulation.Bases[(int)Side.Enemy];
            var before = Meters(20 * Unit, 20 * Unit, target.X, target.Y);
            Order(simulation, ArmyOrder.Base);

            Run(simulation, 60);

            simulation.Soldiers[0].Goal.Should().Be(SoldierGoal.Base);
            Meters(simulation.Soldiers[0].X, simulation.Soldiers[0].Y, target.X, target.Y).Should().BeLessThan(before - 4.5);
        }

        [Test]
        public void Order_Units_HuntsTheNearestEnemySoldierAcrossTheMap()
        {
            var simulation = New();
            simulation.BuildBarracks(2, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 2, 20 * Unit, 20 * Unit);
            // 15 m away: far beyond the sight of the free attack
            simulation.AddSoldiers(Side.Enemy, 1, 50 * Unit, 20 * Unit);
            var enemy = simulation.Soldiers[2].Id;
            Order(simulation, ArmyOrder.Units);

            Run(simulation, 30);

            simulation.Soldiers[0].Goal.Should().Be(SoldierGoal.Soldier);
            simulation.Soldiers[0].GoalId.Should().Be(enemy);
            simulation.Soldiers[0].X.Should().BeGreaterThan(20 * Unit + 3000);
        }

        [Test]
        public void Order_Units_NoEnemySoldiersLeft_RallyByItself()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Player, 2, 20 * Unit, 20 * Unit);
            Order(simulation, ArmyOrder.Units);

            Run(simulation, 2);

            simulation.OrderOf(Side.Player).Should().Be(ArmyOrder.Rally);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.OrderChanged && e.Id == 1 && e.Value == (int)ArmyOrder.Rally);
        }

        [Test]
        public void Order_Barracks_NoneLeft_RallyByItself()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Player, 2, 20 * Unit, 20 * Unit);
            Order(simulation, ArmyOrder.Barracks);

            Run(simulation, 2);

            simulation.OrderOf(Side.Player).Should().Be(ArmyOrder.Rally);
        }

        [Test]
        public void Order_Capture_EachToItsOwnNeutralCell_LandGrows()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Player, 6, 20 * Unit, 20 * Unit);
            var alone = New();
            alone.AddSoldiers(Side.Player, 6, 20 * Unit, 20 * Unit);
            Order(simulation, ArmyOrder.Capture);

            Run(simulation, 3);
            var cells = simulation.Soldiers.Where(s => s.Side == Side.Player).Select(s => s.GoalId).ToList();
            simulation.Soldiers.Should().OnlyContain(s => s.Goal == SoldierGoal.Cell);
            var size = simulation.Grid.Size;
            for (var i = 0; i < cells.Count; i++)
            for (var j = i + 1; j < cells.Count; j++)
            {
                var apart = System.Math.Max(System.Math.Abs(cells[i] % size - cells[j] % size), System.Math.Abs(cells[i] / size - cells[j] / size));
                apart.Should().BeGreaterOrEqualTo(3);
            }

            Run(simulation, 150);
            Run(alone, 153);
            simulation.Grid.Count(Side.Player).Should().BeGreaterThan(alone.Grid.Count(Side.Player));
        }

        [Test]
        public void Order_Enemy_GetsNoOrders()
        {
            var simulation = New();
            simulation.Enqueue(new BattleCommand(1, Side.Enemy, ArmyOrder.Base));

            Run(simulation, 2);

            simulation.OrderOf(Side.Enemy).Should().Be(ArmyOrder.Free);
        }

        [Test]
        public void Order_NewSoldiers_TakeTheCurrentOrder()
        {
            var simulation = New();
            Order(simulation, ArmyOrder.Base);
            Run(simulation, 1);
            simulation.AddSoldiers(Side.Player, 1, 20 * Unit, 20 * Unit);

            Run(simulation, 1);

            simulation.Soldiers[0].Goal.Should().Be(SoldierGoal.Base);
        }

        [Test]
        public void Order_SameScript_SameBattle()
        {
            var a = new BattleSimulation(BattleDeterminismTests.Settings(seed: 5, layer3: true));
            var b = new BattleSimulation(BattleDeterminismTests.Settings(seed: 5, layer3: true));
            foreach (var simulation in new[] { a, b })
            {
                simulation.AddSoldiers(Side.Player, 10, 12 * Unit, 12 * Unit);
                simulation.Enqueue(new BattleCommand(30, Side.Player, ArmyOrder.Capture));
                simulation.Enqueue(new BattleCommand(200, Side.Player, ArmyOrder.Units));
                simulation.Enqueue(new BattleCommand(400, Side.Player, ArmyOrder.Rally));
                Run(simulation, 600);
            }

            a.ComputeHash().Should().Be(b.ComputeHash());
        }
    }
}
