using System.Linq;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;
using FluentAssertions;
using Meta.BattleSystem.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Tests.BattleSystem.Editor
{
    /// <summary>
    /// Layer 5, task 5.1: the battle takes its place from a map — the shape of the land, the bases, the places, the start piles —
    /// and water inside the map works as the sea past the edge (spec 2.2): nobody walks or is pushed into it, it is never painted,
    /// it is out of the shares, and an area touching it is not enclosed.
    /// </summary>
    public sealed class BattleWaterTests
    {
        private const int Unit = BattleSettings.UnitsPerCell;
        // a lake of 12 × 12 cells in the middle of the 64 map: cells 26..37
        private const int LakeFrom = 26;
        private const int LakeTo = 37;

        /// <summary>The square island of stage 1 with a lake in its middle.</summary>
        private static BattleSettings WithLake(bool layer3 = false)
        {
            var settings = BattleDeterminismTests.Settings(layer3: layer3);
            var square = BattleMap.Square(settings);
            var water = new bool[square.Width * square.Height];
            for (var y = LakeFrom; y <= LakeTo; y++)
            for (var x = LakeFrom; x <= LakeTo; x++)
                water[y * square.Width + x] = true;

            var map = new BattleMap(square.Width, square.Height, water);
            map.Bases[(int)Side.Player] = square.Bases[(int)Side.Player];
            map.Bases[(int)Side.Enemy] = square.Bases[(int)Side.Enemy];
            map.Sites.AddRange(square.Sites);
            map.Piles.AddRange(square.Piles);
            settings.Map = map;
            return settings;
        }

        private static bool InLake(int x, int y) =>
            x / Unit >= LakeFrom && x / Unit <= LakeTo && y / Unit >= LakeFrom && y / Unit <= LakeTo;

        private static void Run(BattleSimulation simulation, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                simulation.Step();
        }

        [Test]
        public void Lake_HeroWalksIntoIt_StaysOnTheShore()
        {
            var simulation = new BattleSimulation(WithLake());
            // west of the lake, walking east and a little north for 2 s: it would cross 10 m
            simulation.TeleportHero(Side.Player, 22 * Unit, 30 * Unit);
            simulation.Enqueue(new BattleCommand(1, Side.Player, 1000, 200));

            for (var i = 0; i < 60; i++)
            {
                simulation.Step();
                var hero = simulation.Heroes[0];
                InLake(hero.X - 600, hero.Y).Should().BeFalse();
                InLake(hero.X + 600, hero.Y).Should().BeFalse();
            }

            // slides north along the shore instead of stopping dead
            simulation.Heroes[0].Y.Should().BeGreaterThan(30 * Unit + 1000);
        }

        [Test]
        public void Lake_SoldiersSentAcrossIt_NeverStandInIt()
        {
            var simulation = new BattleSimulation(WithLake(layer3: true));
            simulation.BuildBarracks(1, Side.Enemy);
            // the enemy barracks at (48, 48) lies across the lake from (20, 20)
            simulation.AddSoldiers(Side.Player, 10, 20 * Unit, 20 * Unit);

            for (var i = 0; i < 150; i++)
            {
                simulation.Step();
                foreach (var soldier in simulation.Soldiers)
                    InLake(soldier.X, soldier.Y).Should().BeFalse();
            }
        }

        [Test]
        public void Lake_HeroBrushOnTheShore_DoesNotPaintIt()
        {
            var simulation = new BattleSimulation(WithLake());
            simulation.TeleportHero(Side.Player, LakeFrom * Unit - 200, 30 * Unit);

            Run(simulation, 3);

            simulation.Grid.At(LakeFrom - 1, 30).Should().Be(Side.Player);
            simulation.Grid.At(LakeFrom, 30).Should().Be(Side.Neutral);
            simulation.Grid.IsProtected(30 * 64 + LakeFrom).Should().BeTrue();
        }

        [Test]
        public void Lake_IsNotInTheShares()
        {
            var simulation = new BattleSimulation(WithLake());
            var grid = simulation.Grid;
            var lake = (LakeTo - LakeFrom + 1) * (LakeTo - LakeFrom + 1);

            grid.LandTotal.Should().Be(64 * 64 - lake);
            (grid.Count(Side.Neutral) + grid.Count(Side.Player) + grid.Count(Side.Enemy)).Should().Be(grid.LandTotal);
            simulation.FillShare(Side.Player, 500);
            grid.Permille(Side.Player).Should().Be(500);
            grid.Count(Side.Player).Should().Be((grid.LandTotal * 500 + 999) / 1000);
        }

        [Test]
        public void Lake_AreaClosedByTheLake_IsNotEnclosed()
        {
            // a pocket west of the lake, cells 20..25 × 28..33: the player closes it on three sides, the lake is the fourth
            var simulation = new BattleSimulation(WithLake());
            simulation.TeleportHero(Side.Player, 2 * Unit, 60 * Unit);
            simulation.PaintRect(Side.Player, 19, 27, 25, 27);
            simulation.PaintRect(Side.Player, 19, 34, 25, 34);
            simulation.PaintRect(Side.Player, 19, 27, 19, 34);

            Run(simulation, 2);

            simulation.Grid.At(22, 30).Should().Be(Side.Neutral);
        }

        [Test]
        public void NoLake_SamePocketClosedByThePlayer_IsEnclosed()
        {
            var simulation = new BattleSimulation(BattleDeterminismTests.Settings());
            simulation.TeleportHero(Side.Player, 2 * Unit, 60 * Unit);
            simulation.PaintRect(Side.Player, 19, 27, 26, 27);
            simulation.PaintRect(Side.Player, 19, 34, 26, 34);
            simulation.PaintRect(Side.Player, 19, 27, 19, 34);
            simulation.PaintRect(Side.Player, 26, 27, 26, 34);

            Run(simulation, 2);

            simulation.Grid.At(22, 30).Should().Be(Side.Player);
        }

        [Test]
        public void Lake_NewPiles_NeverOnWater()
        {
            var simulation = new BattleSimulation(WithLake(layer3: true));

            Run(simulation, 1800);

            foreach (var pile in simulation.Resources)
                InLake(pile.X, pile.Y).Should().BeFalse();
        }

        [Test]
        public void Map_GivesBasesPlacesPilesAndStandingBarracks()
        {
            var settings = BattleDeterminismTests.Settings(layer3: true);
            var map = new BattleMap(48);
            map.Bases[(int)Side.Player] = new MapBase(10, 24, 5);
            map.Bases[(int)Side.Enemy] = new MapBase(38, 24, 5);
            map.Sites.Add(new MapSite(20, 10, Side.Neutral));
            map.Sites.Add(new MapSite(28, 38, Side.Enemy));
            map.Piles.Add(new MapPile(ResourceKind.Wood, 14, 30));
            settings.Map = map;

            var simulation = new BattleSimulation(settings);

            simulation.Grid.Width.Should().Be(48);
            simulation.Bases[(int)Side.Player].X.Should().Be(10 * Unit);
            simulation.Bases[(int)Side.Enemy].Y.Should().Be(24 * Unit);
            simulation.Grid.At(10, 24).Should().Be(Side.Player);
            simulation.Grid.At(38, 24).Should().Be(Side.Enemy);
            simulation.Sites.Should().HaveCount(2);
            simulation.Sites[1].Barracks.Should().Be(Side.Enemy);
            simulation.Resources.Should().ContainSingle(r => r.Kind == ResourceKind.Wood && r.Cell == 30 * 48 + 14);
        }

        [Test]
        public void RectangleMap_70By40_PlaysWithinItsBounds()
        {
            // owner, 5 November: a map of the size he sets, a rectangle
            var settings = BattleDeterminismTests.Settings(layer3: true);
            settings.PlayerAutopilot = true;
            var map = new BattleMap(70, 40);
            map.Bases[(int)Side.Player] = new MapBase(8, 8, 6);
            map.Bases[(int)Side.Enemy] = new MapBase(62, 32, 6);
            map.Sites.Add(new MapSite(20, 20, Side.Neutral));
            map.Sites.Add(new MapSite(50, 20, Side.Neutral));
            settings.Map = map;
            var simulation = new BattleSimulation(settings);

            simulation.Grid.Width.Should().Be(70);
            simulation.Grid.Height.Should().Be(40);
            simulation.Grid.Total.Should().Be(2800);
            simulation.Grid.At(62, 32).Should().Be(Side.Enemy);
            for (var i = 0; i < 1800 && simulation.Result == BattleResult.None; i++)
            {
                simulation.Step();
                foreach (var hero in simulation.Heroes)
                {
                    hero.X.Should().BeInRange(0, 70 * Unit);
                    hero.Y.Should().BeInRange(0, 40 * Unit);
                }

                foreach (var soldier in simulation.Soldiers)
                    soldier.Y.Should().BeInRange(0, 40 * Unit);
                foreach (var pile in simulation.Resources)
                    pile.Y.Should().BeLessThan(40 * Unit);
            }
        }

        [Test]
        public void MapFile_Resize_MovesTheMapAndKeepsTheBasesInside()
        {
            var asset = ScriptableObject.CreateInstance<BattleMapAsset>();
            asset.Reset(64, 64);
            asset.SetWater(10, 10, true);
            asset.AddPile(ResourceKind.Gold, new Vector2Int(60, 5));
            asset.AddSite(new Vector2Int(30, 30));

            // grown by 6 cells to the west, cut to 40 up
            asset.Resize(70, 40, 6, 0);

            asset.Width.Should().Be(70);
            asset.Height.Should().Be(40);
            asset.IsWater(16, 10).Should().BeTrue();
            asset.IsWater(2, 10).Should().BeTrue();
            asset.IsWater(20, 20).Should().BeFalse();
            asset.Piles.Should().ContainSingle(p => p.cell == new Vector2Int(66, 5));
            asset.Sites.Should().ContainSingle(s => s.cell == new Vector2Int(36, 30));
            asset.EnemyBase.corner.y.Should().BeLessOrEqualTo(37);
            var back = asset.ToMap();
            back.Width.Should().Be(70);
            back.Height.Should().Be(40);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void SquareMapFromAFile_PlaysAsWithoutOne()
        {
            // the square island written into a map file and read back fights the same battle, tick by tick
            var asset = ScriptableObject.CreateInstance<BattleMapAsset>();
            asset.Set(BattleMap.Square(BattleDeterminismTests.Settings(layer3: true)));
            var plain = new BattleSimulation(new BattleSettings { Seed = 5, PlayerAutopilot = true });
            var filed = new BattleSimulation(new BattleSettings { Seed = 5, PlayerAutopilot = true, Map = asset.ToMap() });

            for (var i = 0; i < 900; i++)
            {
                plain.Step();
                filed.Step();
                filed.ComputeHash().Should().Be(plain.ComputeHash());
            }

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void MapFile_WaterRowsFromTheTop_RoundTrip()
        {
            var water = new bool[48 * 48];
            water[47 * 48 + 0] = true;
            water[5 * 48 + 7] = true;
            var map = new BattleMap(48, water);
            var asset = ScriptableObject.CreateInstance<BattleMapAsset>();

            asset.Set(map);
            var back = asset.ToMap();

            asset.IsWater(0, 47).Should().BeTrue();
            asset.IsWater(7, 5).Should().BeTrue();
            asset.IsWater(8, 5).Should().BeFalse();
            back.Water.Count(w => w).Should().Be(2);
            back.IsWater(7, 5).Should().BeTrue();
            Object.DestroyImmediate(asset);
        }
    }
}
