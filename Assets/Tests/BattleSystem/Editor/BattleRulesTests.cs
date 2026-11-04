using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>The rules of layer 1 of the stage 1 spec: map 2.1, hero 2.2, brush 2.4, enclosure 2.5, victory 2.6.</summary>
    public sealed class BattleRulesTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static BattleSimulation New() => new BattleSimulation(BattleDeterminismTests.Settings());

        private static void Walk(BattleSimulation simulation, int x, int y, int ticks)
        {
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, x, y));
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        /// <summary>Moves the hero of the player far from everything, so its brush does not touch the cells of a test.</summary>
        private static BattleSimulation WithHeroAside()
        {
            var simulation = New();
            Walk(simulation, 0, 0, 1);
            return simulation;
        }

        [Test]
        public void Start_TwoCirclesInOppositeCorners_RestNeutral()
        {
            var simulation = New();
            var grid = simulation.Grid;

            grid.At(7, 7).Should().Be(Side.Player);
            grid.At(56, 56).Should().Be(Side.Enemy);
            grid.At(32, 32).Should().Be(Side.Neutral);
            grid.At(0, 0).Should().Be(Side.Neutral);
            // a radius of 6 cells around (8, 8): the middle of (13, 7) is 5.5 away, of (14, 7) 6.5
            grid.At(13, 7).Should().Be(Side.Player);
            grid.At(14, 7).Should().Be(Side.Neutral);
            grid.Count(Side.Player).Should().Be(grid.Count(Side.Enemy));
        }

        [Test]
        public void Start_HeroInTheMiddleOfTheBlueArea()
        {
            var hero = New().Heroes[0];

            hero.Side.Should().Be(Side.Player);
            hero.X.Should().Be(8 * Unit);
            hero.Y.Should().Be(8 * Unit);
        }

        [Test]
        public void Walk_Diagonal_SameSpeedAsStraight()
        {
            var straight = New();
            var diagonal = New();

            Walk(straight, 1000, 0, 30);
            Walk(diagonal, 1000, 1000, 30);

            var straightPath = (double)straight.Heroes[0].X - 8 * Unit;
            var dx = (double)diagonal.Heroes[0].X - 8 * Unit;
            var dy = (double)diagonal.Heroes[0].Y - 8 * Unit;
            System.Math.Sqrt(dx * dx + dy * dy).Should().BeApproximately(straightPath, straightPath * 0.01);
            // 5 m/s for one second is 10 cells
            straightPath.Should().BeApproximately(10 * Unit, 10 * Unit * 0.01);
        }

        [Test]
        public void Walk_LengthOfDirection_DoesNotChangeSpeed()
        {
            var short_ = New();
            var long_ = New();

            Walk(short_, 10, 0, 30);
            Walk(long_, 1000, 0, 30);

            short_.Heroes[0].X.Should().Be(long_.Heroes[0].X);
        }

        [Test]
        public void Walk_IntoTheEdge_StaysOnTheMapAndSlidesAlong()
        {
            var simulation = New();

            Walk(simulation, -1000, -100, 120);

            var hero = simulation.Heroes[0];
            hero.X.Should().Be(600);
            hero.Y.Should().BeLessThan(8 * Unit).And.BeGreaterOrEqualTo(600);
        }

        [Test]
        public void Brush_Neutral_PaintedWithinTwoCells()
        {
            var simulation = New();

            Walk(simulation, 1000, 1000, 60);

            var hero = simulation.Heroes[0];
            var x = hero.X / Unit;
            var y = hero.Y / Unit;
            simulation.Grid.At(x, y).Should().Be(Side.Player);
            simulation.Grid.At(x + 1, y + 1).Should().Be(Side.Player);
            simulation.Grid.At(x + 3, y + 3).Should().Be(Side.Neutral);
        }

        [Test]
        public void Brush_Red_PaintedLikeNeutral()
        {
            var simulation = New();
            simulation.PaintRect(Side.Enemy, 20, 4, 30, 12);

            Walk(simulation, 1000, 0, 60);

            simulation.Grid.At(24, 8).Should().Be(Side.Player);
            simulation.Grid.At(24, 12).Should().Be(Side.Enemy);
        }

        [Test]
        public void Brush_ProtectedCell_KeepsItsSide()
        {
            var simulation = New();
            var index = 8 * 64 + 11;
            simulation.Grid.Set(index, Side.Enemy);
            simulation.Grid.SetProtected(index, true);

            Walk(simulation, 1000, 0, 15);

            simulation.Grid[index].Should().Be(Side.Enemy);
        }

        [Test]
        public void Enclose_RingAroundNeutral_AreaBecomesBlue()
        {
            var simulation = WithHeroAside();
            Ring(simulation, 20, 20, 30, 30);

            simulation.Step();

            simulation.Grid.At(25, 25).Should().Be(Side.Player);
            simulation.Grid.At(21, 29).Should().Be(Side.Player);
            simulation.Grid.At(31, 25).Should().Be(Side.Neutral);
            simulation.Captures.Should().HaveCount(1);
            simulation.Captures[0].Cells.Should().HaveCount(81);
        }

        [Test]
        public void Enclose_RingAroundPartOfRed_ThatPartBecomesBlue()
        {
            var simulation = WithHeroAside();
            Ring(simulation, 46, 46, 56, 56);

            simulation.Step();

            simulation.Grid.At(52, 52).Should().Be(Side.Player);
            simulation.Grid.At(55, 55).Should().Be(Side.Player);
            simulation.Grid.At(58, 58).Should().Be(Side.Enemy);
        }

        [Test]
        public void Enclose_StripAlongTheEdge_NothingTaken()
        {
            var simulation = WithHeroAside();
            simulation.PaintRect(Side.Player, 20, 0, 20, 10);
            simulation.PaintRect(Side.Player, 20, 10, 40, 10);
            simulation.PaintRect(Side.Player, 40, 0, 40, 10);

            simulation.Step();

            simulation.Grid.At(30, 5).Should().Be(Side.Neutral);
            simulation.Grid.At(30, 0).Should().Be(Side.Neutral);
            simulation.Captures.Should().BeEmpty();
        }

        [Test]
        public void Enclose_ProtectedCellInside_StaysAsItWas()
        {
            var simulation = WithHeroAside();
            var index = 25 * 64 + 25;
            simulation.Grid.Set(index, Side.Enemy);
            simulation.Grid.SetProtected(index, true);
            Ring(simulation, 20, 20, 30, 30);

            simulation.Step();

            simulation.Grid[index].Should().Be(Side.Enemy);
            simulation.Grid.At(24, 25).Should().Be(Side.Player);
        }

        [Test]
        public void Enclose_AreaOverTheLimit_StaysAsItWas()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings(maxEnclosureCells: 80));
            Walk(simulation, 0, 0, 1);
            Ring(simulation, 20, 20, 30, 30);

            simulation.Step();

            simulation.Grid.At(25, 25).Should().Be(Side.Neutral);
        }

        [Test]
        public void Enclose_AreaAtTheLimitWithoutProtectedCells_IsTaken()
        {
            // 81 cells inside the ring, one of them protected: 80 count, as many as the limit
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings(maxEnclosureCells: 80));
            Walk(simulation, 0, 0, 1);
            simulation.Grid.SetProtected(25 * 64 + 25, true);
            Ring(simulation, 20, 20, 30, 30);

            simulation.Step();

            simulation.Grid.At(24, 25).Should().Be(Side.Player);
        }

        [Test]
        public void Enclose_StartingLimit_Is250Cells()
        {
            new BattleSettings().MaxEnclosureCells.Should().Be(250);
        }

        [Test]
        public void Enclose_EveryThirdTick_WaitsForIt()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings(enclosureEveryTicks: 3));
            Walk(simulation, 0, 0, 1);
            Ring(simulation, 20, 20, 30, 30);

            simulation.Step();
            simulation.Grid.At(25, 25).Should().Be(Side.Neutral);
            simulation.Step();

            simulation.Grid.At(25, 25).Should().Be(Side.Player);
        }

        [Test]
        public void Capture_PercentOfTheMap()
        {
            var simulation = WithHeroAside();
            Ring(simulation, 20, 20, 36, 34);

            simulation.Step();

            // 15 × 13 = 195 cells of 4096 (under the limit of 250), 4.76% rounds to 5
            simulation.Captures[0].Percent.Should().Be(5);
        }

        [Test]
        public void Victory_SeventyPercentBlue_BattleEnds()
        {
            var simulation = WithHeroAside();
            simulation.FillShare(Side.Player, 690);
            simulation.Step();
            simulation.Result.Should().Be(BattleResult.None);

            simulation.FillShare(Side.Player, 700);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Victory);
            var tick = simulation.Tick;
            simulation.Step();
            simulation.Tick.Should().Be(tick);
        }

        [Test]
        public void Defeat_SeventyPercentRed_BattleEnds()
        {
            var simulation = WithHeroAside();
            simulation.FillShare(Side.Enemy, 700);

            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Defeat);
        }

        [Test]
        public void FillShare_ReachesTheShareExactly()
        {
            var simulation = New();

            simulation.FillShare(Side.Player, 690);

            simulation.Grid.Count(Side.Player).Should().Be((4096 * 690 + 999) / 1000);
            simulation.Grid.Permille(Side.Player).Should().Be(690);
        }

        private static void Ring(BattleSimulation simulation, int x0, int y0, int x1, int y1)
        {
            simulation.PaintRect(Side.Player, x0, y0, x1, y0);
            simulation.PaintRect(Side.Player, x0, y1, x1, y1);
            simulation.PaintRect(Side.Player, x0, y0, x0, y1);
            simulation.PaintRect(Side.Player, x1, y0, x1, y1);
        }
    }
}
