using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>
    /// Task 4.31, spec 2.4: the brush follows the trail — its middle is where the unit was 0.4 s ago. A hero walking over the
    /// enemy's land leaves the cell under it the enemy's and makes the cells 2 m behind its own; standing still, it has the cell
    /// under it its own in 0.4 s. The same battle on every run.
    /// </summary>
    public sealed class BattleTrailTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;
        private const int Delay = 12;

        private static BattleSimulation Walking(out BattleSettings settings)
        {
            settings = BattleDeterminismTests.Settings();
            settings.BrushDelayTicks = Delay;
            var simulation = new BattleSimulation(settings);
            // the enemy's land from x 10 on; the hero comes from the neutral land west of it, walking east
            simulation.PaintRect(Side.Enemy, 10, 20, 60, 40);
            simulation.TeleportHero(Side.Player, 6 * Unit, 30 * Unit + Unit / 2);
            simulation.Enqueue(new BattleCommand(1, Side.Player, 1000, 0));
            return simulation;
        }

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        private static Side Under(BattleSimulation simulation, int dx = 0)
        {
            var hero = simulation.Heroes.First(h => h.Side == Side.Player);
            return simulation.Grid.At(hero.X / Unit + dx, hero.Y / Unit);
        }

        [Test]
        public void Walking_OnTheEnemysLand_UnderItTheEnemys_TwoMetresBehindItsOwn()
        {
            var simulation = Walking(out _);

            for (var i = 0; i < 60; i++)
            {
                simulation.Step();
                var hero = simulation.Heroes.First(h => h.Side == Side.Player);
                if (hero.X / Unit < 12)
                    continue;

                Under(simulation).Should().Be(Side.Enemy, $"tick {simulation.Tick}");
                Under(simulation, -4).Should().Be(Side.Player, $"tick {simulation.Tick}");
            }
        }

        [Test]
        public void Stopped_TheTrailCatchesUp_TheCellUnderItsOwnIn04s()
        {
            var simulation = Walking(out _);
            Run(simulation, 60);
            simulation.Enqueue(new BattleCommand(simulation.Tick + 1, Side.Player, 0, 0));
            Run(simulation, 2);
            Under(simulation).Should().Be(Side.Enemy);

            Run(simulation, Delay);

            Under(simulation).Should().Be(Side.Player);
        }

        [Test]
        public void Trail_SameScript_SameBattle()
        {
            ulong Hash()
            {
                var settings = BattleDeterminismTests.Settings(layer3: true);
                settings.BrushDelayTicks = Delay;
                settings.PlayerAutopilot = true;
                var simulation = new BattleSimulation(settings);
                Run(simulation, 1500);
                return simulation.ComputeHash();
            }

            Hash().Should().Be(Hash());
        }
    }
}
