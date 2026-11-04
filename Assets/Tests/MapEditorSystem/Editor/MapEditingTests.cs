using FluentAssertions;
using Meta.BattleSystem.Configuration;
using Meta.MapEditorSystem.Services;
using NUnit.Framework;
using UnityEngine;

namespace Tests.MapEditorSystem.Editor
{
    /// <summary>
    /// Layer 5, task 5.2: a new map is a square of land; the brushes of land and water of 1, 3 and 5 cells; a stroke is one step
    /// of the history; undo and redo; the copy differs from the file until it is saved; the file is untouched while editing.
    /// </summary>
    public sealed class MapEditingTests
    {
        private BattleMapAsset _file;
        private MapEditing _editing;

        [SetUp]
        public void SetUp()
        {
            _file = ScriptableObject.CreateInstance<BattleMapAsset>();
            _file.Reset(48);
            _editing = new MapEditing();
            _editing.Open("Карта", _file);
        }

        [TearDown]
        public void TearDown()
        {
            _editing.Close();
            Object.DestroyImmediate(_file);
        }

        [Test]
        public void NewMap_SquareOfLand_BasesInTwoCorners()
        {
            _file.Size.Should().Be(48);
            _file.LandCells().Should().Be(48 * 48);
            _file.PlayerBase.corner.Should().Be(new Vector2Int(8, 8));
            _file.EnemyBase.corner.Should().Be(new Vector2Int(40, 40));
            _file.Sites.Should().BeEmpty();
            _editing.Dirty.Should().BeFalse();
        }

        [TestCase(1, 1)]
        [TestCase(3, 9)]
        [TestCase(5, 25)]
        public void Brush_OfSize_PaintsItsSquare(int brush, int cells)
        {
            _editing.Paint(20, 20, brush, true);

            _editing.Map.LandCells().Should().Be(48 * 48 - cells);
            _editing.Map.IsWater(20, 20).Should().BeTrue();
            _file.LandCells().Should().Be(48 * 48);
        }

        [Test]
        public void Stroke_IsOneStep_UndoAndRedo()
        {
            _editing.PaintLine(10, 10, 30, 10, 1, true);
            _editing.Commit();
            _editing.Paint(5, 40, 3, true);
            _editing.Commit();
            var both = _editing.Map.LandCells();

            _editing.Undo().Should().BeTrue();
            _editing.Map.LandCells().Should().Be(48 * 48 - 21);
            _editing.Undo().Should().BeTrue();
            _editing.Map.LandCells().Should().Be(48 * 48);
            _editing.CanUndo.Should().BeFalse();
            _editing.Redo().Should().BeTrue();
            _editing.Redo().Should().BeTrue();
            _editing.Map.LandCells().Should().Be(both);
            _editing.CanRedo.Should().BeFalse();
        }

        [Test]
        public void LandBrush_OverWater_GivesTheLandBack()
        {
            _editing.Paint(20, 20, 5, true);
            _editing.Commit();

            _editing.Paint(20, 20, 3, false);

            _editing.Map.LandCells().Should().Be(48 * 48 - 16);
            _editing.Map.IsWater(20, 20).Should().BeFalse();
            _editing.Map.IsWater(18, 18).Should().BeTrue();
        }

        [Test]
        public void Dirty_UntilSaved_AndAgainAfterAChange()
        {
            _editing.Paint(1, 1, 1, true);
            _editing.Commit();
            _editing.Dirty.Should().BeTrue();

            _editing.Saved();
            _editing.Dirty.Should().BeFalse();
            _editing.Undo();
            _editing.Dirty.Should().BeTrue();
            _editing.Redo();
            _editing.Dirty.Should().BeFalse();
        }

        [Test]
        public void NewStep_AfterUndo_DropsTheRedo()
        {
            _editing.Paint(1, 1, 1, true);
            _editing.Commit();
            _editing.Undo();

            _editing.Paint(2, 2, 1, true);
            _editing.Commit();

            _editing.CanRedo.Should().BeFalse();
            _editing.Map.IsWater(1, 1).Should().BeFalse();
            _editing.Map.IsWater(2, 2).Should().BeTrue();
        }

        [Test]
        public void EditedMap_PlaysWithItsWater()
        {
            _editing.Paint(24, 24, 5, true);

            var map = _editing.Map.ToMap();

            map.Size.Should().Be(48);
            map.IsWater(24, 24).Should().BeTrue();
            map.IsWater(10, 10).Should().BeFalse();
        }
    }
}
