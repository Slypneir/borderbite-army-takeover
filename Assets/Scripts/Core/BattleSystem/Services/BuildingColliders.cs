using Core.BattleSystem.Data;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// Standing bases and barracks are solid (owner, 4 November): after the heroes and the soldiers walk, any of them inside the
    /// square of a building, grown by the radius of its body, is pushed out along the shorter way, so a unit walking into a
    /// wall slides along it. A fallen building lets everyone through. Integers only, in a fixed order: the same on every device.
    /// </summary>
    public sealed class BuildingColliders
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        public BuildingColliders(BattleState state)
        {
            _state = state;
            _settings = state.Settings;
        }

        public void Step()
        {
            if (!_settings.SolidBuildings)
                return;

            var min = _settings.HeroRadiusUnits;
            var maxX = _settings.MapWidthUnits - _settings.HeroRadiusUnits;
            var maxY = _settings.MapHeightUnits - _settings.HeroRadiusUnits;
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                var fromX = hero.X;
                var fromY = hero.Y;
                if (!hero.Alive || !PushOut(ref hero.X, ref hero.Y, _settings.HeroRadiusUnits))
                    continue;

                hero.X = IntMath.Clamp(hero.X, min, maxX);
                hero.Y = IntMath.Clamp(hero.Y, min, maxY);
                _state.Settle(ref hero.X, ref hero.Y, fromX, fromY, _settings.HeroRadiusUnits);
                _state.Heroes[i] = hero;
            }

            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                var fromX = soldier.X;
                var fromY = soldier.Y;
                if (!PushOut(ref soldier.X, ref soldier.Y, _settings.SoldierBodyUnits))
                    continue;

                soldier.X = IntMath.Clamp(soldier.X, 0, _settings.MapWidthUnits);
                soldier.Y = IntMath.Clamp(soldier.Y, 0, _settings.MapHeightUnits);
                _state.Settle(ref soldier.X, ref soldier.Y, fromX, fromY, _settings.SoldierBodyUnits);
                _state.Soldiers[i] = soldier;
            }
        }

        /// <summary>True when the point was inside a building and was moved out of it.</summary>
        public bool PushOut(ref int x, ref int y, int radius)
        {
            var moved = false;
            foreach (var place in _state.Bases)
            {
                if (place.Standing)
                    moved |= Out(ref x, ref y, place.X, place.Y, _settings.BaseHalfUnits + radius);
            }

            foreach (var site in _state.Sites)
            {
                if (site.Built)
                    moved |= Out(ref x, ref y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY), _settings.BarracksHalfUnits + radius);
            }

            return moved;
        }

        /// <summary>Out of a square of half side <paramref name="half"/> round (cx, cy), by the side nearer to the point.</summary>
        private static bool Out(ref int x, ref int y, int cx, int cy, int half)
        {
            var dx = x - cx;
            var dy = y - cy;
            if (dx <= -half || dx >= half || dy <= -half || dy >= half)
                return false;

            var penetrationX = half - System.Math.Abs(dx);
            var penetrationY = half - System.Math.Abs(dy);
            if (penetrationX < penetrationY)
                x = cx + (dx >= 0 ? half : -half);
            else
                y = cy + (dy >= 0 ? half : -half);
            return true;
        }
    }
}
