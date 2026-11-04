using System.Collections.Generic;
using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// How soldiers walk (stage 1 spec 3.4): to an enemy in sight while not too far from their hero, else to their slot in the
    /// crowd around the hero; they part from each other and from the hero. The slots are a square spiral one meter apart, nearest
    /// first: integers only, the same crowd on every device.
    /// </summary>
    public sealed class ArmyRules
    {
        private readonly BattleState _state;
        private readonly BattleSettings _settings;
        private readonly List<(int X, int Y)> _slots = new List<(int X, int Y)>();

        public ArmyRules(BattleState state)
        {
            _state = state;
            _settings = state.Settings;
            BuildSlots(settings: _settings);
        }

        /// <summary>A new soldier of a side with the health and damage of its side; returns its id.</summary>
        public static int Spawn(BattleState state, Side side, int x, int y)
        {
            var settings = state.Settings;
            var permille = side == Side.Player ? settings.PlayerSoldierPermille : settings.EnemySoldierPermille;
            var id = state.NextId();
            state.Soldiers.Add(new SoldierState
            {
                Id = id,
                Side = side,
                X = IntMath.Clamp(x, 0, settings.MapUnits),
                Y = IntMath.Clamp(y, 0, settings.MapUnits),
                Health = settings.SoldierHealth * permille / 1000,
                Damage = settings.SoldierDamage * permille / 1000,
                AnchorX = x,
                AnchorY = y,
                FacingY = 1,
            });
            return id;
        }

        public void Step()
        {
            var slot = new int[3];
            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                var heroIndex = _state.HeroOf(soldier.Side);
                var hasHero = heroIndex >= 0 && _state.Heroes[heroIndex].Alive;
                var anchorX = hasHero ? _state.Heroes[heroIndex].X : soldier.AnchorX;
                var anchorY = hasHero ? _state.Heroes[heroIndex].Y : soldier.AnchorY;
                var leash2 = (long)_settings.SoldierLeashUnits * _settings.SoldierLeashUnits;

                if (BattleState.Distance2(soldier.X, soldier.Y, anchorX, anchorY) <= leash2 &&
                    CombatRules.NearestTarget(_state, soldier.Side, soldier.X, soldier.Y, _settings.SoldierSightUnits, _settings.SoldierBodyUnits, out var target))
                {
                    // to the enemy, stopping at the reach of a blow
                    if (BattleState.Distance2(soldier.X, soldier.Y, target.X, target.Y) > (long)_settings.SoldierReachUnits * _settings.SoldierReachUnits)
                        Walk(ref soldier, target.X, target.Y, _settings.SoldierReachUnits * 3 / 4);
                    else
                        Face(ref soldier, target.X, target.Y);
                }
                else
                {
                    var (dx, dy) = _slots[slot[(int)soldier.Side]++ % _slots.Count];
                    Walk(ref soldier, anchorX + dx + Jitter(soldier.Id, 0), anchorY + dy + Jitter(soldier.Id, 16), 0);
                }

                _state.Soldiers[i] = soldier;
            }

            Part();
        }

        /// <summary>Every soldier paints its cells; called by side, so the brushes of the player go last.</summary>
        public void Paint(Side side, System.Action<int, int, int, Side> paintCircle)
        {
            foreach (var soldier in _state.Soldiers)
            {
                if (soldier.Side == side)
                    paintCircle(soldier.X, soldier.Y, _settings.SoldierBrushUnits, side);
            }
        }

        /// <summary>A lasting offset of a soldier from its slot, up to 30 % of the spacing: a crowd, not a parade.</summary>
        private int Jitter(int id, int shift)
        {
            var hash = (uint)id * 2654435761u;
            var range = _settings.SlotSpacingUnits * 3 / 10;
            return (int)((hash >> shift) % (uint)(range * 2 + 1)) - range;
        }

        private void Walk(ref SoldierState soldier, int x, int y, int stopAt)
        {
            long dx = x - soldier.X;
            long dy = y - soldier.Y;
            var length = IntMath.Sqrt(dx * dx + dy * dy);
            if (length <= stopAt || length == 0)
                return;

            var step = System.Math.Min(_settings.SoldierStepUnits, length - stopAt);
            soldier.X += (int)(dx * step / length);
            soldier.Y += (int)(dy * step / length);
            soldier.FacingX = (int)(dx * 1000 / length);
            soldier.FacingY = (int)(dy * 1000 / length);
        }

        private static void Face(ref SoldierState soldier, int x, int y)
        {
            if (x == soldier.X && y == soldier.Y)
                return;

            soldier.FacingX = x - soldier.X;
            soldier.FacingY = y - soldier.Y;
        }

        /// <summary>Soldiers too close push each other apart by half the overlap each and give way to their hero; all stay on the map.</summary>
        private void Part()
        {
            var spacing = _settings.SoldierSpacingUnits;
            var soldiers = _state.Soldiers;
            for (var i = 0; i < soldiers.Count; i++)
            {
                var a = soldiers[i];
                for (var j = i + 1; j < soldiers.Count; j++)
                {
                    var b = soldiers[j];
                    long dx = b.X - a.X;
                    long dy = b.Y - a.Y;
                    var distance = IntMath.Sqrt(dx * dx + dy * dy);
                    if (distance >= spacing)
                        continue;

                    if (distance == 0)
                    {
                        // the same point: part along x, the earlier one to the left
                        dx = 1;
                        distance = 1;
                    }

                    var push = (spacing - distance) / 2 + 1;
                    a.X -= (int)(dx * push / distance);
                    a.Y -= (int)(dy * push / distance);
                    b.X += (int)(dx * push / distance);
                    b.Y += (int)(dy * push / distance);
                    soldiers[j] = b;
                }

                GiveWay(ref a);
                a.X = IntMath.Clamp(a.X, _settings.HeroRadiusUnits, _settings.MapUnits - _settings.HeroRadiusUnits);
                a.Y = IntMath.Clamp(a.Y, _settings.HeroRadiusUnits, _settings.MapUnits - _settings.HeroRadiusUnits);
                soldiers[i] = a;
            }
        }

        private void GiveWay(ref SoldierState soldier)
        {
            var heroIndex = _state.HeroOf(soldier.Side);
            if (heroIndex < 0 || !_state.Heroes[heroIndex].Alive)
                return;

            var hero = _state.Heroes[heroIndex];
            var room = _settings.SoldierSpacingUnits;
            long dx = soldier.X - hero.X;
            long dy = soldier.Y - hero.Y;
            var distance = IntMath.Sqrt(dx * dx + dy * dy);
            if (distance >= room)
                return;

            if (distance == 0)
            {
                dy = -1;
                distance = 1;
            }

            soldier.X = hero.X + (int)(dx * room / distance);
            soldier.Y = hero.Y + (int)(dy * room / distance);
        }

        private void BuildSlots(BattleSettings settings)
        {
            var spacing = settings.SlotSpacingUnits;
            const int rings = 4;
            for (var y = -rings; y <= rings; y++)
            for (var x = -rings; x <= rings; x++)
            {
                if (x != 0 || y != 0)
                    _slots.Add((x * spacing, y * spacing));
            }

            // nearest first; behind the hero (negative y) before in front at the same distance, then left before right
            _slots.Sort((a, b) =>
            {
                var da = (long)a.X * a.X + (long)a.Y * a.Y;
                var db = (long)b.X * b.X + (long)b.Y * b.Y;
                if (da != db)
                    return da.CompareTo(db);
                return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
        }
    }
}
