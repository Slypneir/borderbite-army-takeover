using System.Collections.Generic;
using System.Linq;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using NUnit.Framework;

namespace Tests.BattleSystem.Editor
{
    /// <summary>
    /// Tasks 4.29 and 4.30 (stage 1 spec 3.1): a new resource never appears within 2 m of the edge of a place to build or of the
    /// walls of a base; new wood appears only while some place is free to build on, gold always.
    /// </summary>
    public sealed class BattleResourceTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;

        private static long Gap(int x, int y, int cx, int cy, int half)
        {
            long dx = System.Math.Max(0, System.Math.Abs(x - cx) - half);
            long dy = System.Math.Max(0, System.Math.Abs(y - cy) - half);
            return (long)System.Math.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void NewResources_ManySeeds_NeverByAPlaceOrABase()
        {
            for (uint seed = 1; seed <= 12; seed++)
            {
                var settings = BattleDeterminismTests.Settings(seed, layer3: true);
                settings.PlayerAutopilot = true;
                var simulation = new BattleSimulation(settings);
                var known = new HashSet<int>(simulation.Resources.Select(r => r.Id));
                var keep = settings.ResourceKeepOffUnits;
                for (var tick = 0; tick < 1800 && simulation.Result == BattleResult.None; tick++)
                {
                    simulation.Step();
                    foreach (var resource in simulation.Resources)
                    {
                        if (!known.Add(resource.Id))
                            continue;

                        foreach (var site in simulation.Sites)
                            Gap(resource.X, resource.Y, site.CenterX * Unit + Unit / 2, site.CenterY * Unit + Unit / 2, Unit * 3 / 2)
                                .Should().BeGreaterOrEqualTo(keep, $"seed {seed}, a pile at {resource.X / Unit};{resource.Y / Unit}");
                        foreach (var place in simulation.Bases.Where(b => b.Standing))
                            Gap(resource.X, resource.Y, place.X, place.Y, settings.BaseHalfUnits)
                                .Should().BeGreaterOrEqualTo(keep, $"seed {seed}, a pile at {resource.X / Unit};{resource.Y / Unit}");
                    }
                }
            }
        }

        [Test]
        public void Wood_EveryPlaceBuilt_NoNewWood_ADestroyedBarracks_WoodAgain()
        {
            var settings = BattleDeterminismTests.Settings();
            var simulation = new BattleSimulation(settings);
            for (var i = 0; i < simulation.Sites.Count; i++)
                simulation.BuildBarracks(i, i % 2 == 0 ? Side.Player : Side.Enemy);
            simulation.TeleportHero(Side.Player, 32 * Unit, 2 * Unit);
            var wood = new HashSet<int>(simulation.Resources.Where(r => r.Kind == ResourceKind.Wood).Select(r => r.Id));
            var gold = simulation.Resources.Count(r => r.Kind == ResourceKind.Gold);

            for (var i = 0; i < 20 * settings.WoodEveryTicks; i++)
                simulation.Step();

            simulation.Resources.Where(r => r.Kind == ResourceKind.Wood).Should().OnlyContain(r => wood.Contains(r.Id));
            simulation.Resources.Count(r => r.Kind == ResourceKind.Gold).Should().BeGreaterThan(gold);

            simulation.DestroyBarracks(3);
            for (var i = 0; i < 3 * settings.WoodEveryTicks; i++)
                simulation.Step();

            simulation.Resources.Should().Contain(r => r.Kind == ResourceKind.Wood && !wood.Contains(r.Id));
        }
    }
}
