using System.Collections.Generic;
using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    public sealed class BattleDeterminismTests
    {
        private const int Ticks = 3000;

        /// <summary>The numbers of the stage 1 spec in units: 64 cells, 5 m/s at 30 ticks and 0.5 m cells is 333 units a tick.</summary>
        /// <summary>
        /// Without layer 3 by default — no enemy hero, the hero starts in the middle of its base, no aura of the bases, enemy
        /// soldiers at ×1.0 — so the rules of layers 1 and 2 are checked alone; <paramref name="layer3"/> gives the whole battle.
        /// </summary>
        public static BattleSettings Settings(uint seed = 777, int enclosureEveryTicks = 1, int maxEnclosureCells = 250, bool layer3 = false)
        {
            var settings = new BattleSettings { Seed = seed, EnclosureEveryTicks = enclosureEveryTicks, MaxEnclosureCells = maxEnclosureCells };
            if (layer3)
                return settings;

            settings.EnemyHero = false;
            settings.BaseGateUnits = 0;
            settings.BaseAuraCells = 0;
            settings.EnemySoldierPermille = 1000;
            return settings;
        }

        /// <summary>
        /// A walk that turns every 13 ticks and crosses the map: paints, encloses, hits the edges, picks resources up; with an army,
        /// barracks of both sides and enemies that fight it.
        /// </summary>
        private static void EnqueueScript(BattleSimulation simulation)
        {
            simulation.Give(Side.Player, 200, 40);
            simulation.BuildBarracks(0, Side.Player);
            simulation.BuildBarracks(1, Side.Enemy);
            simulation.AddSoldiers(Side.Player, 12, 9000, 9000);
            simulation.AddSoldiers(Side.Enemy, 10, 30000, 30000);
            int[] xs = { 1000, 700, 0, -1000, -300, 0, 600 };
            int[] ys = { 0, 700, 1000, 200, -1000, -1000, 300 };
            for (var tick = 1; tick < Ticks - 100; tick += 13)
                simulation.Enqueue(new BattleCommand(tick, Side.Player, xs[tick % 7], ys[tick % 7]));
        }

        /// <summary>Runs on frames of the given lengths and returns the hash after every tick.</summary>
        private static List<ulong> Run(IEnumerable<long> frames, int maxSteps = 16)
        {
            var runner = new BattleRunner(Settings(layer3: true), maxSteps);
            EnqueueScript(runner.Simulation);
            var hashes = new List<ulong>();
            runner.OnStep += () => hashes.Add(runner.Simulation.ComputeHash());
            foreach (var frame in frames)
            {
                runner.Advance(frame);
                if (hashes.Count >= Ticks || runner.Simulation.Result != BattleResult.None)
                    break;
            }

            return hashes;
        }

        private static IEnumerable<long> Frames(int hz)
        {
            while (true)
                yield return 1000000L / hz;
        }

        [Test]
        public void Run_At60And144And165Hz_SameHashAtEveryTick()
        {
            var at60 = Run(Frames(60));
            var at144 = Run(Frames(144));
            var at165 = Run(Frames(165));

            at60.Count.Should().BeGreaterThan(300);
            at144.Take(Ticks).Should().Equal(at60.Take(Ticks));
            at165.Take(Ticks).Should().Equal(at60.Take(Ticks));
        }

        [Test]
        public void Run_UnevenFrames_SameHashAtEveryTick()
        {
            var uneven = Run(Uneven());

            uneven.Take(Ticks).Should().Equal(Run(Frames(60)).Take(Ticks));
        }

        private static IEnumerable<long> Uneven()
        {
            var lengths = new long[] { 4000, 33000, 6060, 120000, 16666, 1000, 50000 };
            for (var i = 0; ; i++)
                yield return lengths[i % lengths.Length];
        }

        [Test]
        public void Run_OtherSeed_OtherHash()
        {
            var one = new BattleSimulation(Settings(1));
            var two = new BattleSimulation(Settings(2));
            one.Step();
            two.Step();

            one.ComputeHash().Should().NotBe(two.ComputeHash());
        }

        [Test]
        public void Enqueue_PastTick_Refused()
        {
            var simulation = new BattleSimulation(Settings());
            simulation.Step();

            simulation.Enqueue(new BattleCommand(1, Side.Player, 1000, 0)).Should().BeFalse();
            simulation.Enqueue(new BattleCommand(2, Side.Player, 1000, 0)).Should().BeTrue();
        }

        [Test]
        public void Assembly_DoesNotReferenceUnity()
        {
            var references = typeof(BattleSimulation).Assembly.GetReferencedAssemblies().Select(a => a.Name);

            references.Should().NotContain(name => name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor"));
        }
    }

    public sealed class FixedStepClockTests
    {
        [TestCase(60)]
        [TestCase(144)]
        [TestCase(165)]
        public void Advance_TenSeconds_ThreeHundredStepsAtAnyRate(int hz)
        {
            var clock = new FixedStepClock(30, 8);
            var steps = 0;
            for (var i = 0; i < hz * 10; i++)
                steps += clock.Advance(1000000L * (i + 1) / hz - 1000000L * i / hz);

            steps.Should().Be(300);
        }

        [Test]
        public void Advance_LongHitch_CappedAndDropsTheRest()
        {
            var clock = new FixedStepClock(30, 8);

            clock.Advance(10000000).Should().Be(8);
            clock.Advance(0).Should().Be(0);
        }

        [Test]
        public void Alpha_HalfTick_IsHalf()
        {
            var clock = new FixedStepClock(10, 8);

            clock.Advance(50000);

            clock.Alpha.Should().BeApproximately(0.5, 0.0001);
        }
    }
}
