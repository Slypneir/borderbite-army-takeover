using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>The rules of layer 2 of the stage 1 spec: resources 3.1, places 3.2, barracks 3.3, army 3.4, the hero fights 3.5.</summary>
    public sealed class BattleArmyTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static BattleSimulation New() => new BattleSimulation(BattleDeterminismTests.Settings());

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        private static void Walk(BattleSimulation simulation, int x, int y, int ticks)
        {
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, x, y));
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1 + ticks, Side.Player, 0, 0));
            Run(simulation, ticks);
        }

        /// <summary>From the start the hero walks onto place 0 at (15, 15): 7.5 cells along both axes.</summary>
        private static void WalkOntoFirstPlace(BattleSimulation simulation) => Walk(simulation, 1000, 1000, 32);

        private static double Meters(int x0, int y0, int x1, int y1) =>
            System.Math.Sqrt((double)(x1 - x0) * (x1 - x0) + (double)(y1 - y0) * (y1 - y0)) / (2 * Unit);

        [Test]
        public void Start_ThreeGoldAndTwoWoodAtEachStartArea_OnNeutralCells()
        {
            var simulation = New();

            simulation.Resources.Count(r => r.Kind == ResourceKind.Gold).Should().Be(6);
            simulation.Resources.Count(r => r.Kind == ResourceKind.Wood).Should().Be(4);
            simulation.Resources.Should().OnlyContain(r => simulation.Grid[r.Cell] == Side.Neutral);
        }

        [Test]
        public void Spawn_ThirtySeconds_TwelveGoldEightWoodOnGreyAwayFromTheHero()
        {
            var simulation = New();

            Run(simulation, 900);

            var hero = simulation.Heroes[0];
            simulation.Resources.Count(r => r.Kind == ResourceKind.Gold).Should().Be(12);
            simulation.Resources.Count(r => r.Kind == ResourceKind.Wood).Should().Be(8);
            simulation.Resources.Should().OnlyContain(r => simulation.Grid[r.Cell] == Side.Neutral);
            simulation.Resources.Select(r => r.Cell).Should().OnlyHaveUniqueItems();
            simulation.Resources.Should().OnlyContain(r => Meters(r.X, r.Y, hero.X, hero.Y) >= 2.0);
        }

        [Test]
        public void PickUp_HeroWalksOverAPile_PurseGrows()
        {
            var simulation = New();

            // the pile of gold at (15, 9): the hero walks right along y = 8
            Walk(simulation, 1000, 0, 30);

            simulation.SideOf(Side.Player).Gold.Should().Be(5);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.Pickup && e.Value == 5);
        }

        [Test]
        public void Place_BelongsToTheColourOfItsCentre()
        {
            var simulation = New();
            simulation.Grid.At(15, 15).Should().Be(Side.Neutral);

            WalkOntoFirstPlace(simulation);

            simulation.Grid.At(15, 15).Should().Be(Side.Player);
        }

        [Test]
        public void Build_StandOneSecondWithTenWood_BarracksAndAuraOfTheSide()
        {
            var simulation = New();
            simulation.Give(Side.Player, 0, 10);
            WalkOntoFirstPlace(simulation);

            Run(simulation, 31);

            simulation.Sites[0].Barracks.Should().Be(Side.Player);
            simulation.SideOf(Side.Player).Wood.Should().Be(0);
            simulation.Grid.IsProtected(15 * 64 + 18).Should().BeTrue();
            simulation.Grid.At(18, 15).Should().Be(Side.Player);
        }

        [Test]
        public void Build_NotEnoughWood_NothingBuilt()
        {
            var simulation = New();
            simulation.Give(Side.Player, 0, 9);
            WalkOntoFirstPlace(simulation);

            Run(simulation, 60);

            simulation.Sites[0].Built.Should().BeFalse();
            simulation.SideOf(Side.Player).Wood.Should().Be(9);
        }

        [Test]
        public void Aura_EnemyPaint_CellsStay()
        {
            var simulation = New();
            simulation.BuildBarracks(4, Side.Player);
            var site = simulation.Sites[4];

            simulation.PaintRect(Side.Enemy, site.CenterX - 3, site.CenterY - 3, site.CenterX + 3, site.CenterY + 3);

            simulation.Grid.At(site.CenterX, site.CenterY + 3).Should().Be(Side.Player);
            simulation.Grid.At(site.CenterX + 3, site.CenterY + 3).Should().Be(Side.Enemy);
        }

        [Test]
        public void Hire_HeroAtItsBarracks_ASoldierEveryHalfSecondForFiveGold()
        {
            var simulation = New();
            simulation.Give(Side.Player, 50, 10);
            WalkOntoFirstPlace(simulation);
            Run(simulation, 31);

            Run(simulation, 150);

            // the way to the place passes the pile of gold at (13, 13)
            var picked = simulation.Events.Where(e => e.Kind == EventKind.Pickup && e.Value == 5).Sum(e => e.Value);
            simulation.ArmyOf(Side.Player).Should().Be(10);
            simulation.SideOf(Side.Player).Gold.Should().Be(50 + picked - 10 * 5);
        }

        [Test]
        public void Hire_ArmyAtTheLimit_NoMore()
        {
            var simulation = New();
            simulation.Give(Side.Player, 1000, 10);
            WalkOntoFirstPlace(simulation);

            Run(simulation, 31 + 15 * 35);

            simulation.ArmyOf(Side.Player).Should().Be(30);
        }

        [Test]
        public void Soldiers_OfThePlayer_TenPercentStronger()
        {
            var simulation = New();

            simulation.AddSoldiers(Side.Player, 1, 30000, 30000);
            simulation.AddSoldiers(Side.Enemy, 1, 40000, 40000);

            simulation.Soldiers[0].Health.Should().Be(330);
            simulation.Soldiers[0].Damage.Should().Be(55);
            simulation.Soldiers[1].Health.Should().Be(300);
        }

        [Test]
        public void Army_FollowsTheHero_WithinOneToFourMetersAndApart()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Player, 20, 8000, 8000);

            Walk(simulation, 1000, 300, 60);
            Run(simulation, 60);

            var hero = simulation.Heroes[0];
            foreach (var soldier in simulation.Soldiers)
                Meters(soldier.X, soldier.Y, hero.X, hero.Y).Should().BeInRange(0.45, 4.3);

            var soldiers = simulation.Soldiers.ToList();
            for (var i = 0; i < soldiers.Count; i++)
            for (var j = i + 1; j < soldiers.Count; j++)
                Meters(soldiers[i].X, soldiers[i].Y, soldiers[j].X, soldiers[j].Y).Should().BeGreaterThan(0.3);
        }

        [Test]
        public void Army_PaintsWithBrushesOfOneCell()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Player, 10, 8000, 8000);
            var before = simulation.Grid.Count(Side.Player);
            var alone = New();

            Walk(simulation, 1000, 0, 90);
            Walk(alone, 1000, 0, 90);

            (simulation.Grid.Count(Side.Player) - before).Should().BeGreaterThan(alone.Grid.Count(Side.Player) - before);
        }

        [Test]
        public void Fight_TenRedSoldiersAtTheHero_HeroFightsDiesAndComesBackHome()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Enemy, 10, 9000, 7000);

            Run(simulation, 120);

            simulation.Events.Should().Contain(e => e.Kind == EventKind.Strike && e.Side == Side.Player);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.HeroDied);
            Run(simulation, 120);
            var respawn = simulation.Events.First(e => e.Kind == EventKind.HeroRespawned);
            respawn.X.Should().Be(8 * Unit);
            respawn.Y.Should().Be(8 * Unit);
        }

        [Test]
        public void Fight_GunOn_ReachesSixMeters()
        {
            var settings = BattleDeterminismTests.Settings();
            settings.HeroGun = true;
            var simulation = new BattleSimulation(settings);
            // 5 m to the right of the hero, out of the sword's reach, out of the soldier's sight of 4 m
            simulation.AddSoldiers(Side.Enemy, 1, 8000 + 10000, 8000);

            Run(simulation, 2);

            simulation.Events.Should().Contain(e => e.Kind == EventKind.Shot && e.Side == Side.Player);
        }

        [Test]
        public void Fight_GunOffByDefault_NoShotsOnlyTheSword()
        {
            // task 4.8: for now both heroes fight only close by
            var simulation = New();
            simulation.AddSoldiers(Side.Enemy, 1, 8000 + 10000, 8000);

            Run(simulation, 60);

            new BattleSettings().HeroGun.Should().BeFalse();
            simulation.Events.Should().NotContain(e => e.Kind == EventKind.Shot);
        }

        [Test]
        public void Fight_EnemyBarracksWithoutDefenders_FallsToSoldiers()
        {
            var simulation = New();
            // place 0 is 5 m from the hero: within the leash of 8 m of its soldiers
            simulation.BuildBarracks(0, Side.Enemy);
            var site = simulation.Sites[0];
            simulation.AddSoldiers(Side.Player, 10, site.CenterX * Unit, (site.CenterY - 3) * Unit);

            Run(simulation, 600);

            simulation.Sites[0].Built.Should().BeFalse();
            simulation.Grid.IsProtected(site.CenterY * 64 + site.CenterX).Should().BeFalse();
            simulation.Events.Should().Contain(e => e.Kind == EventKind.Destroyed);
        }

        [Test]
        public void Heal_HeroOnItsLand_FivePointsASecondThreeSecondsAfterTheLastBlow()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Enemy, 1, 8000 + 1500, 8000);
            // three blows of the sword in a second kill it; it strikes back meanwhile
            Run(simulation, 40);
            var hero = simulation.Heroes[0];
            var hurt = hero.Health;
            hurt.Should().BeLessThan(1000);
            simulation.Soldiers.Should().BeEmpty();

            // no healing for 3 s after the last blow (task 4.7)
            Run(simulation, hero.HurtTick + 89 - simulation.Tick);
            simulation.Heroes[0].Health.Should().Be(hurt);

            Run(simulation, 30);
            simulation.Heroes[0].Health.Should().Be(System.Math.Min(1000, hurt + 50));
        }

        [Test]
        public void Heal_HeroOnItsLandUnderBlows_LosesHealth()
        {
            // the hero cannot strike back, the blows go on: at home it must still lose health (task 4.7)
            var settings = BattleDeterminismTests.Settings();
            settings.MeleeDamage = 0;
            settings.GunDamage = 0;
            var simulation = new BattleSimulation(settings);
            simulation.AddSoldiers(Side.Enemy, 1, 8000 + 1500, 8000);
            Run(simulation, 30);
            var after1 = simulation.Heroes[0].Health;

            Run(simulation, 150);

            simulation.Grid.At(8, 8).Should().Be(Side.Player);
            simulation.Heroes[0].Health.Should().BeLessThan(after1 - 200);
        }
    }
}
