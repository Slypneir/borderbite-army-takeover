using Core.BattleSystem.Data;
using Core.BattleSystem.Enums;
using Core.BattleSystem.Structs;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// Gold and wood (stage 1 spec 3.1), places to build and barracks (3.2, 3.3), hiring and the healing of a hero on its own
    /// land (3.5). Random places come from the seed of the level.
    /// </summary>
    public sealed class EconomyRules
    {
        private const int SpawnAttempts = 24;

        private readonly BattleState _state;
        private readonly BattleSettings _settings;

        public EconomyRules(BattleState state)
        {
            _state = state;
            _settings = state.Settings;
        }

        /// <summary>The start piles at the edges of both start areas, the enemy's mirrored through the centre.</summary>
        public void LayStartPiles()
        {
            var size = _state.Grid.Width;
            foreach (var pile in _state.Map.Piles)
                Add(pile.Kind, pile.Y * size + pile.X, pile.Kind == ResourceKind.Gold ? _settings.GoldPortion : _settings.WoodPortion);
        }

        public void Step()
        {
            Spawn(ResourceKind.Gold, _settings.GoldEveryTicks, _settings.GoldMax, _settings.GoldPortion);
            // wood only while there is a place left to build on (task 4.30); the wood lying stays
            if (AnyPlaceFree())
                Spawn(ResourceKind.Wood, _settings.WoodEveryTicks, _settings.WoodMax, _settings.WoodPortion);
            Build();
            Hire();
            Heal();
        }

        /// <summary>Builds a barracks of a side on a place at once: the start of a level and the commands of the developer.</summary>
        public void BuildBarracks(int site, Side side)
        {
            ref var place = ref _state.Sites[site];
            place.Barracks = side;
            place.Health = _settings.BarracksHealth;
            place.BuildProgress = 0;
            ForAura(place, index =>
            {
                _state.Grid.Set(index, side);
                _state.Grid.SetProtected(index, true);
            });
            _state.Emit(EventKind.Built, side, site, 0, BattleState.Middle(place.CenterX), BattleState.Middle(place.CenterY));
        }

        /// <summary>The aura of a base: its colour around it, protected while it stands (spec 4.1, by the rules of a barracks).</summary>
        public void ProtectBase(Side side)
        {
            var place = _state.Bases[(int)side];
            var size = _state.Grid.Width;
            var radius = _settings.BaseAuraCells;
            var cx = place.X / BattleSettings.UnitsPerCell;
            var cy = place.Y / BattleSettings.UnitsPerCell;
            // the middle of the base is a corner of four cells: a cell is in when its middle is within the radius
            for (var y = cy - radius - 1; y <= cy + radius; y++)
            for (var x = cx - radius - 1; x <= cx + radius; x++)
            {
                var dx = 2 * x + 1 - 2 * cx;
                var dy = 2 * y + 1 - 2 * cy;
                if (!_state.Grid.Inside(x, y) || dx * dx + dy * dy > 4 * radius * radius)
                    continue;

                _state.Grid.Set(y * size + x, side);
                _state.Grid.SetProtected(y * size + x, place.Standing);
            }
        }

        /// <summary>The barracks falls: the aura goes, the place is grey again.</summary>
        public void Destroy(int site)
        {
            ref var place = ref _state.Sites[site];
            var side = place.Barracks;
            place.Barracks = Side.Neutral;
            place.Health = 0;
            ForAura(place, index => _state.Grid.SetProtected(index, false));
            for (var y = place.CenterY - 1; y <= place.CenterY + 1; y++)
            for (var x = place.CenterX - 1; x <= place.CenterX + 1; x++)
                _state.Grid.Set(y * _state.Grid.Width + x, Side.Neutral);
            _state.Emit(EventKind.Destroyed, side, site, 0, BattleState.Middle(place.CenterX), BattleState.Middle(place.CenterY));
        }

        /// <summary>
        /// A blow of mining (spec 3.1, task 4.19): the nearest pile within the reach of the hero gives one unit to its side and
        /// is gone after its last; the unit is told as a pickup of 1. False when no pile is in reach. Only heroes mine.
        /// </summary>
        public bool Mine(HeroState hero, out int x, out int y)
        {
            x = 0;
            y = 0;
            var reach = (long)_settings.HeroRadiusUnits + _settings.MineReachUnits;
            var best = reach * reach + 1;
            var index = -1;
            for (var i = 0; i < _state.Resources.Count; i++)
            {
                var resource = _state.Resources[i];
                var distance = BattleState.Distance2(hero.X, hero.Y, resource.X, resource.Y);
                if (distance >= best)
                    continue;

                best = distance;
                index = i;
            }

            if (index < 0)
                return false;

            var pile = _state.Resources[index];
            ref var purse = ref _state.Sides[(int)hero.Side];
            if (pile.Kind == ResourceKind.Gold)
                purse.Gold++;
            else
                purse.Wood++;

            pile.Amount--;
            x = pile.X;
            y = pile.Y;
            _state.Emit(EventKind.Pickup, hero.Side, pile.Id, 1, pile.X, pile.Y, hero.X, hero.Y, (int)pile.Kind);
            if (pile.Amount > 0)
                _state.Resources[index] = pile;
            else
                _state.Resources.RemoveAt(index);
            return true;
        }

        /// <summary>A new pile every period while there are fewer than the most: on a neutral cell, or on any unprotected one
        /// when little neutral land is left; never close to a hero or a soldier.</summary>
        private void Spawn(ResourceKind kind, int every, int most, int portion)
        {
            if (_state.Tick % every != 0 || Count(kind) >= most)
                return;

            var grid = _state.Grid;
            var anyLand = grid.Permille(Side.Neutral) < _settings.NeutralLowPermille;
            for (var attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                var cell = _state.Random.Next(grid.Total);
                if (grid.IsProtected(cell) || (!anyLand && grid[cell] != Side.Neutral) || Taken(cell) || NearSomeone(cell) || NearBuilding(cell))
                    continue;

                Add(kind, cell, portion);
                return;
            }
        }

        /// <summary>
        /// A hero with the wood for it builds on an empty place it stands near, unless the place is of the other side's colour
        /// (owner, 5 November: as it mines a pile or strikes an enemy near it; the place need not be painted first). The player's
        /// hero comes first when both are near; the progress starts anew when the builder changes or steps away.
        /// </summary>
        private void Build()
        {
            for (var s = 0; s < _state.Sites.Length; s++)
            {
                ref var site = ref _state.Sites[s];
                var builder = site.Built ? Side.Neutral : BuilderOf(site);
                if (builder == Side.Neutral || builder != site.Builder)
                {
                    site.Builder = builder;
                    site.BuildProgress = 0;
                    if (builder == Side.Neutral)
                        continue;
                }

                site.BuildProgress++;
                if (site.BuildProgress < _settings.BuildTicks)
                    continue;

                _state.Sides[(int)builder].Wood -= _settings.BarracksWood;
                site.Builder = Side.Neutral;
                BuildBarracks(s, builder);
            }
        }

        private static readonly Side[] Builders = { Side.Player, Side.Enemy };

        private Side BuilderOf(SiteState site)
        {
            var colour = _state.Grid.At(site.CenterX, site.CenterY);
            foreach (var side in Builders)
            {
                var hero = _state.HeroOf(side);
                if (colour == BattleState.Enemy(side) || hero < 0 || !_state.Heroes[hero].Alive || !ByPlace(_state.Heroes[hero], site) ||
                    _state.Sides[(int)side].Wood < _settings.BarracksWood || (side == Side.Enemy && _state.Tick < _settings.EnemyBuildFromTicks))
                    continue;

                return side;
            }

            return Side.Neutral;
        }

        /// <summary>While the hero stands near its own barracks, a soldier every period for gold, up to the limit of the army.</summary>
        private void Hire()
        {
            var hire = (long)_settings.HireUnits + _settings.HeroRadiusUnits + _settings.BarracksHalfUnits;
            var reach2 = hire * hire;
            foreach (var hero in _state.Heroes)
            {
                ref var purse = ref _state.Sides[(int)hero.Side];
                var barracks = hero.Alive ? NearestBarracks(hero, reach2) : -1;
                if (barracks < 0)
                {
                    purse.HireTimer = 0;
                    continue;
                }

                purse.HireTimer++;
                if (purse.HireTimer < _settings.HireEveryOf(hero.Side))
                    continue;

                purse.HireTimer = 0;
                if (purse.Gold < _settings.SoldierGold || _state.ArmyOf(hero.Side) >= _state.ArmyCapOf(hero.Side))
                    continue;

                purse.Gold -= _settings.SoldierGold;
                var site = _state.Sites[barracks];
                // out of the door (south), clear of the walls: a barracks is solid
                var door = _settings.BarracksHalfUnits + _settings.SoldierBodyUnits + BattleSettings.UnitsPerCell / 4;
                var id = ArmyRules.Spawn(_state, hero.Side, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY) - door);
                _state.Emit(EventKind.Hired, hero.Side, id, barracks, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
            }
        }

        /// <summary>
        /// Heroes and soldiers heal a point at a time on any land, but only out of a fight: not hurt and not striking for a
        /// while (T16) — the player's hero 1 s, the enemy hero and every soldier 2 s (task 4.14). The first point comes then, the
        /// next ones every so many ticks: the player's hero every 0.5 s, the others every 1 s (owner, 4 November).
        /// </summary>
        private void Heal()
        {
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                if (!hero.Alive || hero.Health >= _settings.HeroHealthOf(hero.Side) || !HealsNow(hero.FightTick, _settings.HeroHealAfterOf(hero.Side), _settings.HeroHealEveryOf(hero.Side)))
                    continue;

                hero.Health++;
                _state.Heroes[i] = hero;
            }

            for (var i = 0; i < _state.Soldiers.Count; i++)
            {
                var soldier = _state.Soldiers[i];
                if (soldier.Health >= _settings.SoldierHealthOf(soldier.Side) || !HealsNow(soldier.FightTick, _settings.HealAfterTicks, _settings.HealEveryTicks))
                    continue;

                soldier.Health++;
                _state.Soldiers[i] = soldier;
            }
        }

        private bool HealsNow(int fightTick, int after, int every)
        {
            var calm = _state.Tick - fightTick - after;
            return calm >= 0 && calm % System.Math.Max(1, every) == 0;
        }

        private int NearestBarracks(HeroState hero, long reach2)
        {
            var best = -1;
            var bestDistance = long.MaxValue;
            for (var s = 0; s < _state.Sites.Length; s++)
            {
                var site = _state.Sites[s];
                if (site.Barracks != hero.Side)
                    continue;

                // the reach of hiring is from the edge of the hero to the walls of the barracks
                var distance = BattleState.Distance2(hero.X, hero.Y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
                if (distance <= reach2 && distance < bestDistance)
                {
                    best = s;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// The hero is near the place: the edge of its body within the reach of building from the walls of the barracks to be,
        /// the same «near» as hiring (owner, 5 November: a hero standing by the place builds, it need not step on it).
        /// </summary>
        private bool ByPlace(HeroState hero, SiteState site)
        {
            var reach = (long)_settings.BarracksHalfUnits + _settings.HeroRadiusUnits + _settings.BuildReachUnits;
            var distance = BattleState.Distance2(hero.X, hero.Y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
            return distance <= reach * reach;
        }

        private void ForAura(SiteState site, System.Action<int> visit)
        {
            var size = _state.Grid.Width;
            var radius = _settings.AuraCells;
            for (var y = site.CenterY - radius; y <= site.CenterY + radius; y++)
            for (var x = site.CenterX - radius; x <= site.CenterX + radius; x++)
            {
                var dx = x - site.CenterX;
                var dy = y - site.CenterY;
                if (_state.Grid.Inside(x, y) && dx * dx + dy * dy <= radius * radius)
                    visit(y * size + x);
            }
        }

        private void Add(ResourceKind kind, int cell, int amount)
        {
            var size = _state.Grid.Width;
            _state.Resources.Add(new ResourceState
            {
                Id = _state.NextId(),
                Kind = kind,
                Cell = cell,
                X = BattleState.Middle(cell % size),
                Y = BattleState.Middle(cell / size),
                Amount = amount,
            });
        }

        private int Count(ResourceKind kind)
        {
            var count = 0;
            foreach (var resource in _state.Resources)
            {
                if (resource.Kind == kind)
                    count++;
            }

            return count;
        }

        private bool Taken(int cell)
        {
            foreach (var resource in _state.Resources)
            {
                if (resource.Cell == cell)
                    return true;
            }

            return false;
        }

        private bool AnyPlaceFree()
        {
            foreach (var site in _state.Sites)
            {
                if (!site.Built)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The middle of a cell within the keep-off of the edge of a place to build (3 by 3 cells), empty or not, or of the walls of
        /// a base (task 4.29).
        /// </summary>
        private bool NearBuilding(int cell)
        {
            var size = _state.Grid.Width;
            var x = BattleState.Middle(cell % size);
            var y = BattleState.Middle(cell / size);
            var keep = _settings.ResourceKeepOffUnits;
            foreach (var site in _state.Sites)
            {
                if (Within(x, y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY), BattleSettings.UnitsPerCell * 3 / 2, keep))
                    return true;
            }

            foreach (var place in _state.Bases)
            {
                if (place.Standing && Within(x, y, place.X, place.Y, _settings.BaseHalfUnits, keep))
                    return true;
            }

            return false;
        }

        /// <summary>A point within <paramref name="reach"/> of a square of half side <paramref name="half"/> round (cx, cy), or in it.</summary>
        private static bool Within(int x, int y, int cx, int cy, int half, int reach)
        {
            long dx = System.Math.Max(0, System.Math.Abs(x - cx) - half);
            long dy = System.Math.Max(0, System.Math.Abs(y - cy) - half);
            return dx * dx + dy * dy < (long)reach * reach;
        }

        private bool NearSomeone(int cell)
        {
            var size = _state.Grid.Width;
            var x = BattleState.Middle(cell % size);
            var y = BattleState.Middle(cell / size);
            var away2 = (long)_settings.ResourceAwayUnits * _settings.ResourceAwayUnits;
            foreach (var hero in _state.Heroes)
            {
                if (hero.Alive && BattleState.Distance2(x, y, hero.X, hero.Y) < away2)
                    return true;
            }

            foreach (var soldier in _state.Soldiers)
            {
                if (BattleState.Distance2(x, y, soldier.X, soldier.Y) < away2)
                    return true;
            }

            return false;
        }
    }
}
