using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>Land cut off from its base and barracks goes back to grass (owner, 4 November), within the enclosure limit.</summary>
    public sealed class BattleCutOffTests
    {
        private static BattleSimulation New()
        {
            var settings = BattleDeterminismTests.Settings(layer3: true);
            settings.EnemyHero = false;
            return new BattleSimulation(settings);
        }

        [Test]
        public void CutOff_APieceAwayFromTheBase_GoesBackToGrass()
        {
            var simulation = New();
            simulation.PaintRect(Side.Enemy, 30, 10, 34, 14);

            simulation.Step();

            simulation.Grid.At(32, 12).Should().Be(Side.Neutral);
            simulation.Captures.Should().Contain(c => c.Side == Side.Neutral && c.Cells.Length == 25);
        }

        [Test]
        public void CutOff_APieceJoinedToTheBase_Stays()
        {
            var simulation = New();
            // a strip from the start area of the player out to the piece
            simulation.PaintRect(Side.Player, 8, 8, 34, 8);
            simulation.PaintRect(Side.Player, 30, 9, 34, 14);

            simulation.Step();

            simulation.Grid.At(32, 12).Should().Be(Side.Player);
        }

        [Test]
        public void CutOff_APieceByItsBarracks_Stays()
        {
            var simulation = New();
            simulation.BuildBarracks(1, Side.Player);
            var site = simulation.Sites[1];

            simulation.Step();

            simulation.Grid.At(site.CenterX, site.CenterY).Should().Be(Side.Player);
            simulation.Grid.At(site.CenterX + 3, site.CenterY).Should().Be(Side.Player);
        }

        [Test]
        public void CutOff_APieceLargerThanTheLimit_StaysWhole()
        {
            var simulation = New();
            // 16 × 17 = 272 cells, over the limit of 250
            simulation.PaintRect(Side.Player, 30, 20, 45, 36);

            simulation.Step();

            simulation.Grid.At(38, 28).Should().Be(Side.Player);
        }

        [Test]
        public void CutOff_TheLineThatSplitsAPiece_TheFarHalfGoes()
        {
            var simulation = New();
            // the enemy reaches out from its base along a strip; the player paints across it
            simulation.PaintRect(Side.Enemy, 40, 30, 56, 32);
            simulation.PaintRect(Side.Enemy, 54, 33, 56, 55);
            simulation.Step();
            simulation.Grid.At(42, 31).Should().Be(Side.Enemy);

            simulation.PaintRect(Side.Player, 46, 28, 47, 34);
            simulation.Step();

            simulation.Grid.At(42, 31).Should().Be(Side.Neutral);
            simulation.Grid.At(52, 31).Should().Be(Side.Enemy);
        }

        [Test]
        public void CutOff_OffInTheRulesOfLayersOneAndTwo()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings());
            simulation.PaintRect(Side.Player, 30, 10, 34, 14);

            simulation.Step();

            simulation.Grid.At(32, 12).Should().Be(Side.Player);
            simulation.Captures.Should().NotContain(c => c.Side == Side.Neutral);
        }
    }
}
