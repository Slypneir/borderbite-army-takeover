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
            // the soldiers go for the enemy base on their own: a base that cannot fall keeps the battle going
            var settings = BattleDeterminismTests.Settings();
            settings.BaseHealth = 100000;
            var simulation = new BattleSimulation(settings);
            simulation.Give(Side.Player, 1000, 10);
            WalkOntoFirstPlace(simulation);

            Run(simulation, 31 + 15 * 35);

            simulation.ArmyOf(Side.Player).Should().Be(30);
        }

        [Test]
        public void Soldiers_BothSides_FivePointsNoFactors()
        {
            var settings = BattleDeterminismTests.Settings();
            settings.PlayerSoldierBonusHealth = 2;
            var simulation = new BattleSimulation(settings);

            simulation.AddSoldiers(Side.Player, 1, 30000, 30000);
            simulation.AddSoldiers(Side.Enemy, 1, 40000, 40000);

            new BattleSettings().SoldierHealth.Should().Be(5);
            simulation.Soldiers[0].Health.Should().Be(7);
            simulation.Soldiers[1].Health.Should().Be(5);
        }

        [Test]
        public void Army_NoEnemyNear_WalksToTheNearestEnemyBarracksAndLeavesAStripe()
        {
            // owner, 4 November: the soldier does not follow the hero; place 2 at (12, 36) is nearer than place 1 at (48, 48)
            var simulation = New();
            simulation.BuildBarracks(1, Side.Enemy);
            simulation.BuildBarracks(2, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 1, 11 * Unit, 12 * Unit);

            Walk(simulation, 1000, 0, 90);

            var soldier = simulation.Soldiers[0];
            soldier.Goal.Should().Be(SoldierGoal.Barracks);
            soldier.GoalId.Should().Be(2);
            Meters(soldier.X, soldier.Y, 12 * Unit + Unit / 2, 36 * Unit + Unit / 2).Should().BeLessThan(3.5);
            for (var y = 16; y <= 26; y += 2)
                simulation.Grid.At(11, y).Should().Be(Side.Player);
        }

        [Test]
        public void Army_NoBarracks_WalksToTheEnemyBase()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Player, 1, 20 * Unit, 20 * Unit);
            var target = simulation.Bases[(int)Side.Enemy];
            var before = Meters(20 * Unit, 20 * Unit, target.X, target.Y);

            Run(simulation, 60);

            simulation.Soldiers[0].Goal.Should().Be(SoldierGoal.Base);
            Meters(simulation.Soldiers[0].X, simulation.Soldiers[0].Y, target.X, target.Y).Should().BeLessThan(before - 4.5);
        }

        [Test]
        public void Army_EnemyWithinEightMeters_TurnsOnIt()
        {
            var simulation = New();
            simulation.BuildBarracks(1, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 1, 30 * Unit, 20 * Unit);
            // 7 m to the left, off the way to the barracks; another 10 m below: out of sight
            simulation.AddSoldiers(Side.Enemy, 1, 16 * Unit, 20 * Unit);
            simulation.AddSoldiers(Side.Enemy, 1, 30 * Unit, 0);

            Run(simulation, 1);

            var soldier = simulation.Soldiers[0];
            soldier.Goal.Should().Be(SoldierGoal.Soldier);
            soldier.GoalId.Should().Be(simulation.Soldiers[1].Id);
            soldier.X.Should().BeLessThan(30 * Unit);
        }

        [Test]
        public void Army_TargetFalls_TakesTheNext()
        {
            var simulation = New();
            simulation.BuildBarracks(1, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 3, 30 * Unit, 20 * Unit);
            simulation.AddSoldiers(Side.Enemy, 1, 33 * Unit, 20 * Unit);

            Run(simulation, 150);

            simulation.Soldiers.Should().OnlyContain(s => s.Side == Side.Player);
            simulation.Soldiers.Should().OnlyContain(s => s.Goal == SoldierGoal.Barracks && s.GoalId == 1);
        }

        [Test]
        public void Army_HeroWalksAway_SoldiersKeepTheirWayApart()
        {
            var simulation = New();
            simulation.BuildBarracks(1, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 20, 10 * Unit, 10 * Unit);

            Walk(simulation, -1000, 1000, 60);

            var hero = simulation.Heroes[0];
            var soldiers = simulation.Soldiers.ToList();
            soldiers.Should().OnlyContain(s => s.Goal == SoldierGoal.Barracks);
            for (var i = 0; i < soldiers.Count; i++)
            {
                Meters(soldiers[i].X, soldiers[i].Y, hero.X, hero.Y).Should().BeGreaterThan(0.45);
                for (var j = i + 1; j < soldiers.Count; j++)
                    Meters(soldiers[i].X, soldiers[i].Y, soldiers[j].X, soldiers[j].Y).Should().BeGreaterThan(0.3);
            }
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
            // place 0 is the nearest enemy barracks: the soldiers go for it on their own
            simulation.BuildBarracks(0, Side.Enemy);
            var site = simulation.Sites[0];
            simulation.AddSoldiers(Side.Player, 10, site.CenterX * Unit, (site.CenterY - 3) * Unit);

            Run(simulation, 600);

            simulation.Sites[0].Built.Should().BeFalse();
            simulation.Grid.IsProtected(site.CenterY * 64 + site.CenterX).Should().BeFalse();
            simulation.Events.Should().Contain(e => e.Kind == EventKind.Destroyed);
        }

        [Test]
        public void Heal_PlayerHero_APointEveryHalfSecondThreeSecondsAfterTheFight()
        {
            var simulation = New();
            simulation.AddSoldiers(Side.Enemy, 1, 8000 + 1500, 8000);
            // five blows of the sword kill it; it strikes back meanwhile
            Run(simulation, 90);
            var hero = simulation.Heroes[0];
            var hurt = hero.Health;
            hurt.Should().BeLessThan(10);
            simulation.Soldiers.Should().BeEmpty();

            // T16: not hurt and not striking for 3 s, then a point at once and a point every 0.5 s
            Run(simulation, hero.FightTick + 89 - simulation.Tick);
            simulation.Heroes[0].Health.Should().Be(hurt);
            Run(simulation, 1);
            simulation.Heroes[0].Health.Should().Be(hurt + 1);
            Run(simulation, 15);
            simulation.Heroes[0].Health.Should().Be(System.Math.Min(10, hurt + 2));
        }

        [Test]
        public void Heal_EnemySoldier_APointASecondAfterTheFight()
        {
            var settings = BattleDeterminismTests.Settings();
            settings.SoldierStepUnits = 0;
            var simulation = new BattleSimulation(settings);
            simulation.AddSoldiers(Side.Enemy, 1, 40 * Unit, 40 * Unit);
            simulation.TeleportHero(Side.Player, 40 * Unit + 1500, 40 * Unit);
            Run(simulation, 1);
            simulation.Soldiers[0].Health.Should().Be(4);
            var fight = simulation.Soldiers[0].FightTick;
            simulation.TeleportHero(Side.Player, 60 * Unit, 10 * Unit);

            Run(simulation, fight + 89 - simulation.Tick);
            simulation.Soldiers[0].Health.Should().Be(4);
            Run(simulation, 1);
            simulation.Soldiers[0].Health.Should().Be(5);
        }

        [Test]
        public void Heal_HeroUnderBlows_LosesHealth()
        {
            // the hero cannot hurt the soldier, the blows go on: it must lose health on its own land (T16)
            var settings = BattleDeterminismTests.Settings();
            settings.MeleeDamage = 0;
            settings.GunDamage = 0;
            var simulation = new BattleSimulation(settings);
            simulation.AddSoldiers(Side.Enemy, 1, 8000 + 1500, 8000);
            Run(simulation, 30);
            var after1 = simulation.Heroes[0].Health;

            Run(simulation, 150);

            simulation.Grid.At(8, 8).Should().Be(Side.Player);
            simulation.Heroes[0].Health.Should().BeLessThan(after1 - 4);
        }
    }
}
