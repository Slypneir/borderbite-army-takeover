using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>
    /// Owner, 4 November: a hero right by something acts on it whatever way it faces — strikes an enemy, a building, builds on
    /// a place — and enemies come before buildings. Every reach is from the edge of a body to the edge of another.
    /// </summary>
    public sealed class BattleReachTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        [Test]
        public void Heroes_SideBySideFacingAway_TheyFight()
        {
            // both heroes stand still 1.6 m apart (beyond 1.2 m between their middles), facing the camera
            var settings = BattleDeterminismTests.Settings(layer3: true);
            var simulation = new BattleSimulation(settings);
            simulation.SetEnemyStrength(800, 100000, 900, 100000);
            Run(simulation, 1);
            simulation.TeleportHero(Side.Player, 20 * Unit, 20 * Unit);
            simulation.TeleportHero(Side.Enemy, 20 * Unit + 3200, 20 * Unit);

            Run(simulation, 3);

            simulation.Events.Should().Contain(e => e.Kind == EventKind.Strike && e.Side == Side.Player && e.Id < 0);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.Strike && e.Side == Side.Enemy && e.Id < 0);
        }

        [Test]
        public void Build_HeroByTheEdgeOfThePlace_Builds()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings());
            var site = simulation.Sites[0];
            simulation.PaintRect(Side.Player, site.CenterX - 1, site.CenterY - 1, site.CenterX + 1, site.CenterY + 1);
            simulation.Give(Side.Player, 0, 10);
            // 1.3 m from the middle: off the 3 × 3 place (0.75 m), its body by the edge
            simulation.TeleportHero(Side.Player, BattleStateMiddle(site.CenterX) + 2600, BattleStateMiddle(site.CenterY));

            Run(simulation, 31);

            simulation.Sites[0].Barracks.Should().Be(Side.Player);
        }

        [Test]
        public void Strike_HeroAtTheWallOfABarracks_HitsIt()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings());
            simulation.BuildBarracks(1, Side.Enemy);
            var site = simulation.Sites[1];
            // 2.5 m from the middle of a barracks 2.25 m wide: by its wall
            simulation.TeleportHero(Side.Player, BattleStateMiddle(site.CenterX) - 5000, BattleStateMiddle(site.CenterY));

            Run(simulation, 2);

            simulation.Events.Should().Contain(e => e.Kind == EventKind.BuildingHit && e.Side == Side.Enemy && e.Id == 1);
        }

        [Test]
        public void Strike_EnemyAndBuildingBothNear_TheEnemyFirst()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings());
            simulation.BuildBarracks(1, Side.Enemy);
            var site = simulation.Sites[1];
            var x = BattleStateMiddle(site.CenterX) - 4000;
            var y = BattleStateMiddle(site.CenterY);
            simulation.TeleportHero(Side.Player, x, y);
            simulation.AddSoldiers(Side.Enemy, 1, x - 1500, y);

            Run(simulation, 1);

            var blow = simulation.Events.First(e => e.Kind == EventKind.Strike && e.Side == Side.Player && e.Id < 0);
            blow.ToX.Should().BeLessThan(x);
            simulation.Events.Should().NotContain(e => e.Kind == EventKind.BuildingHit);
        }

        private static int BattleStateMiddle(int cell) => cell * Unit + Unit / 2;
    }
}
