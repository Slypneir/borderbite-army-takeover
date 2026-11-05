using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>
    /// Task 4.36, spec 2.6a «Второй шанс»: the first side at 50% of the map gets the second chance, the other takes it at 51%; a
    /// side with it whose base falls fights on without respawns; it loses for good when the chance is taken away or nothing of it
    /// is left; a base that falls without the chance is a defeat, as before. The same rule for both sides.
    /// </summary>
    public sealed class BattleSecondChanceTests
    {
        private static BattleSimulation New() => new BattleSimulation(BattleDeterminismTests.Settings(layer3: true));

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        private static bool HeroAlive(BattleSimulation simulation, Side side) => simulation.Heroes.Any(h => h.Side == side && h.Alive);

        /// <summary>The player takes half of the map and the chance with it.</summary>
        private static BattleSimulation PlayerHolds()
        {
            var simulation = New();
            simulation.FillShare(Side.Player, 520);
            simulation.Step();
            simulation.SecondChance.Should().Be(Side.Player);
            return simulation;
        }

        [Test]
        public void Chance_NeitherAtHalf_NoOne()
        {
            var simulation = New();
            simulation.FillShare(Side.Player, 450);

            simulation.Step();

            simulation.SecondChance.Should().Be(Side.Neutral);
        }

        [Test]
        public void Chance_FirstAtHalf_GetsIt()
        {
            var simulation = New();
            simulation.FillShare(Side.Enemy, 520);

            simulation.Step();

            simulation.SecondChance.Should().Be(Side.Enemy);
            simulation.Events.Should().ContainSingle(e => e.Kind == EventKind.SecondChance && e.Side == Side.Enemy && e.Value == (int)Side.Neutral);
        }

        [Test]
        public void Chance_OtherAtHalf_KeepsIt_At51_TakesIt()
        {
            var simulation = PlayerHolds();

            // the enemy at the middle is not enough: the step of 1% keeps the chance where it is
            simulation.FillShare(Side.Enemy, 503);
            simulation.Step();
            simulation.SecondChance.Should().Be(Side.Player);

            simulation.FillShare(Side.Enemy, 530);
            simulation.Step();

            simulation.SecondChance.Should().Be(Side.Enemy);
            simulation.Events.Should().Contain(e => e.Kind == EventKind.SecondChance && e.Side == Side.Enemy && e.Value == (int)Side.Player);
        }

        [Test]
        public void BaseFalls_WithTheChance_TheBattleGoesOn_NoRespawn()
        {
            var simulation = PlayerHolds();
            simulation.AddSoldiers(Side.Player, 2, 4000, 4000);

            simulation.SetBaseHealth(Side.Player, 0);
            simulation.Step();
            simulation.Result.Should().Be(BattleResult.None);

            simulation.HurtHero(Side.Player, 1000);
            Run(simulation, 150);

            HeroAlive(simulation, Side.Player).Should().BeFalse();
            simulation.Events.Should().NotContain(e => e.Kind == EventKind.HeroRespawned && e.Side == Side.Player);
        }

        [Test]
        public void BaseFalls_WithoutTheChance_Defeat()
        {
            var simulation = New();
            simulation.FillShare(Side.Enemy, 520);
            simulation.Step();

            simulation.SetBaseHealth(Side.Player, 0);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Defeat);
        }

        [Test]
        public void NoBase_TheChanceTakenAway_Defeat()
        {
            var simulation = PlayerHolds();
            simulation.SetBaseHealth(Side.Player, 0);
            simulation.Step();
            simulation.Result.Should().Be(BattleResult.None);

            simulation.FillShare(Side.Enemy, 530);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Defeat);
        }

        [Test]
        public void NoBase_NoHeroAndNoSoldiers_Defeat()
        {
            var simulation = PlayerHolds();
            simulation.SetBaseHealth(Side.Player, 0);
            simulation.Step();
            simulation.Result.Should().Be(BattleResult.None);

            simulation.HurtHero(Side.Player, 1000);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Defeat);
        }

        [Test]
        public void EnemyBaseFalls_WithTheChance_NoVictoryUntilItIsTaken()
        {
            var simulation = New();
            simulation.FillShare(Side.Enemy, 520);
            simulation.Step();

            simulation.SetBaseHealth(Side.Enemy, 0);
            simulation.Step();
            simulation.Result.Should().Be(BattleResult.None);

            simulation.FillShare(Side.Player, 530);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Victory);
        }

        [Test]
        public void NoBase_EightyPercent_StillWins()
        {
            var simulation = PlayerHolds();
            simulation.SetBaseHealth(Side.Player, 0);
            simulation.Step();

            // over 80 by a margin: the enemy paints a few cells on the same tick
            simulation.FillShare(Side.Player, 820);
            simulation.Step();

            simulation.Result.Should().Be(BattleResult.Victory);
        }
    }
}
