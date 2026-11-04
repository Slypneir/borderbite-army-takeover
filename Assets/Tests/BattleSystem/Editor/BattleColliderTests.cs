using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>Bases and barracks are solid (owner, 4 November): units stop at their walls and slide along them.</summary>
    public sealed class BattleColliderTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static void Walk(BattleSimulation simulation, int x, int y, int ticks)
        {
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, x, y));
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        private static int Middle(int cell) => cell * Unit + Unit / 2;

        /// <summary>The rules of layers 1–2 alone, with solid buildings.</summary>
        private static BattleSettings Solid()
        {
            var settings = BattleDeterminismTests.Settings();
            settings.SolidBuildings = true;
            return settings;
        }

        [Test]
        public void Hero_WalksIntoABarracks_StopsAtItsWall()
        {
            var simulation = new BattleSimulation(Solid());
            simulation.BuildBarracks(1, Side.Enemy);
            var site = simulation.Sites[1];
            simulation.TeleportHero(Side.Player, Middle(site.CenterX) - 6000, Middle(site.CenterY));

            Walk(simulation, 1000, 0, 60);

            var hero = simulation.Heroes[0];
            var wall = Middle(site.CenterX) - simulation.Settings.BarracksHalfUnits - simulation.Settings.HeroRadiusUnits;
            hero.X.Should().Be(wall);
        }

        [Test]
        public void Hero_WalksIntoABaseAtAnAngle_SlidesAlongItsWall()
        {
            var settings = BattleDeterminismTests.Settings(layer3: true);
            settings.EnemyHero = false;
            var simulation = new BattleSimulation(settings);
            var home = simulation.Bases[(int)Side.Player];
            simulation.TeleportHero(Side.Player, home.X + 6000, home.Y - 1000);

            // to the west and a little north: against the east wall, it slides north along it
            Walk(simulation, -1000, 300, 30);

            var hero = simulation.Heroes[0];
            hero.X.Should().BeGreaterOrEqualTo(home.X + settings.BaseHalfUnits + settings.HeroRadiusUnits);
            hero.Y.Should().BeGreaterThan(home.Y - 1000);
        }

        [Test]
        public void Barracks_Falls_NoMoreWall()
        {
            var simulation = new BattleSimulation(Solid());
            simulation.BuildBarracks(1, Side.Enemy);
            var site = simulation.Sites[1];
            simulation.TeleportHero(Side.Player, Middle(site.CenterX) - 4000, Middle(site.CenterY));
            // the sword brings 300 health down in 15 s
            for (var i = 0; i < 16 * 30 && simulation.Sites[1].Built; i++)
                simulation.Step();
            simulation.Sites[1].Built.Should().BeFalse();

            Walk(simulation, 1000, 0, 30);

            simulation.Heroes[0].X.Should().BeGreaterThan(Middle(site.CenterX));
        }

        [Test]
        public void Hire_SoldierAppears_OutsideTheWalls()
        {
            var simulation = new BattleSimulation(Solid());
            simulation.BuildBarracks(0, Side.Player);
            simulation.Give(Side.Player, 10, 0);
            var site = simulation.Sites[0];
            simulation.TeleportHero(Side.Player, Middle(site.CenterX), Middle(site.CenterY) - 4000);

            for (var i = 0; i < 20; i++)
                simulation.Step();

            simulation.Soldiers.Should().NotBeEmpty();
            var soldier = simulation.Soldiers[0];
            var half = simulation.Settings.BarracksHalfUnits;
            (System.Math.Abs(soldier.X - Middle(site.CenterX)) >= half || System.Math.Abs(soldier.Y - Middle(site.CenterY)) >= half).Should().BeTrue();
        }
    }
}
