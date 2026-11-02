using System.Collections.Generic;
using System.Linq;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    public sealed class BattleDeterminismTests
    {
        private const int Ticks = 3000;
        private const int TicksPerSecond = 30;

        private static BattleSettings Settings(uint seed = 777) => new BattleSettings(TicksPerSecond, 12, 3, 10, 30, 60, 15, seed);

        private static void EnqueueScript(BattleSimulation simulation)
        {
            for (var tick = 7; tick < Ticks - 100; tick += 13)
                simulation.Enqueue(new BattleCommand(tick, tick % 3, tick % 12, (tick * 5 + 1) % 12));
        }

        /// <summary>Runs on frames of the given lengths and returns the hash after every tick.</summary>
        private static List<ulong> Run(IEnumerable<long> frames, int maxSteps = 16)
        {
            var runner = new BattleRunner(Settings(), maxSteps);
            EnqueueScript(runner.Simulation);
            var hashes = new List<ulong>();
            runner.OnStep += () => hashes.Add(runner.Simulation.ComputeHash());
            foreach (var frame in frames)
            {
                runner.Advance(frame);
                if (hashes.Count >= Ticks)
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

            at60.Count.Should().BeGreaterOrEqualTo(Ticks);
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
        public void Run_OtherSeed_OtherResult()
        {
            var one = new BattleSimulation(Settings(1));
            var two = new BattleSimulation(Settings(2));
            one.Enqueue(new BattleCommand(1, 0, 0, 1));
            two.Enqueue(new BattleCommand(1, 0, 0, 1));
            one.Step();
            two.Step();

            one.ComputeHash().Should().NotBe(two.ComputeHash());
        }

        [Test]
        public void Enqueue_PastTick_Refused()
        {
            var simulation = new BattleSimulation(Settings());
            simulation.Step();

            simulation.Enqueue(new BattleCommand(1, 0, 0, 1)).Should().BeFalse();
            simulation.Enqueue(new BattleCommand(2, 0, 0, 1)).Should().BeTrue();
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
