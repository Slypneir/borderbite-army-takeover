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
            var last = _settings.MapCells - 1;
            foreach (var side in new[] { Side.Player, Side.Enemy })
            {
                Lay(_settings.StartGoldCells, ResourceKind.Gold, side == Side.Enemy, last);
                Lay(_settings.StartWoodCells, ResourceKind.Wood, side == Side.Enemy, last);
            }
        }

        public void Step()
        {
            PickUp();
            Spawn(ResourceKind.Gold, _settings.GoldEveryTicks, _settings.GoldMax, _settings.GoldPortion);
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
            var size = _state.Grid.Size;
            var radius = _settings.BaseAuraCells;
            var cx = place.X / BattleSettings.UnitsPerCell;
            var cy = place.Y / BattleSettings.UnitsPerCell;
            // the middle of the base is a corner of four cells: a cell is in when its middle is within the radius
            for (var y = cy - radius - 1; y <= cy + radius; y++)
            for (var x = cx - radius - 1; x <= cx + radius; x++)
            {
                var dx = 2 * x + 1 - 2 * cx;
                var dy = 2 * y + 1 - 2 * cy;
                if (x < 0 || y < 0 || x >= size || y >= size || dx * dx + dy * dy > 4 * radius * radius)
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
                _state.Grid.Set(y * _state.Grid.Size + x, Side.Neutral);
            _state.Emit(EventKind.Destroyed, side, site, 0, BattleState.Middle(place.CenterX), BattleState.Middle(place.CenterY));
        }

        private void Lay(int[] cells, ResourceKind kind, bool mirrored, int last)
        {
            var portion = kind == ResourceKind.Gold ? _settings.GoldPortion : _settings.WoodPortion;
            for (var i = 0; i + 1 < cells.Length; i += 2)
            {
                var x = mirrored ? last - cells[i] : cells[i];
                var y = mirrored ? last - cells[i + 1] : cells[i + 1];
                Add(kind, y * _state.Grid.Size + x, portion);
            }
        }

        private void PickUp()
        {
            var reach2 = (long)_settings.PickupUnits * _settings.PickupUnits;
            foreach (var hero in _state.Heroes)
            {
                if (!hero.Alive)
                    continue;

                for (var i = 0; i < _state.Resources.Count; i++)
                {
                    var resource = _state.Resources[i];
                    if (BattleState.Distance2(hero.X, hero.Y, resource.X, resource.Y) > reach2)
                        continue;

                    ref var purse = ref _state.Sides[(int)hero.Side];
                    if (resource.Kind == ResourceKind.Gold)
                        purse.Gold += resource.Amount;
                    else
                        purse.Wood += resource.Amount;

                    _state.Emit(EventKind.Pickup, hero.Side, resource.Id, resource.Amount, resource.X, resource.Y, hero.X, hero.Y, (int)resource.Kind);
                    _state.Resources.RemoveAt(i);
                    i--;
                }
            }
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
                if (grid.IsProtected(cell) || (!anyLand && grid[cell] != Side.Neutral) || Taken(cell) || NearSomeone(cell))
                    continue;

                Add(kind, cell, portion);
                return;
            }
        }

        private void Build()
        {
            for (var s = 0; s < _state.Sites.Length; s++)
            {
                ref var site = ref _state.Sites[s];
                var owner = _state.Grid.At(site.CenterX, site.CenterY);
                var hero = owner == Side.Neutral ? -1 : _state.HeroOf(owner);
                var standing = !site.Built && hero >= 0 && _state.Heroes[hero].Alive && OnSite(_state.Heroes[hero], site) &&
                               _state.Sides[(int)owner].Wood >= _settings.BarracksWood &&
                               (owner != Side.Enemy || _state.Tick >= _settings.EnemyBuildFromTicks);
                if (!standing)
                {
                    site.BuildProgress = 0;
                    continue;
                }

                site.BuildProgress++;
                if (site.BuildProgress < _settings.BuildTicks)
                    continue;

                _state.Sides[(int)owner].Wood -= _settings.BarracksWood;
                BuildBarracks(s, owner);
            }
        }

        /// <summary>While the hero stands near its own barracks, a soldier every period for gold, up to the limit of the army.</summary>
        private void Hire()
        {
            var reach2 = (long)_settings.HireUnits * _settings.HireUnits;
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
                if (purse.HireTimer < _settings.HireEveryTicks)
                    continue;

                purse.HireTimer = 0;
                if (purse.Gold < _settings.SoldierGold || _state.ArmyOf(hero.Side) >= _settings.ArmyMax)
                    continue;

                purse.Gold -= _settings.SoldierGold;
                var site = _state.Sites[barracks];
                var id = ArmyRules.Spawn(_state, hero.Side, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY) - BattleSettings.UnitsPerCell * 2);
                _state.Emit(EventKind.Hired, hero.Side, id, barracks, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
            }
        }

        /// <summary>
        /// A hero on the land of its side heals by the rate per second, counted exactly in ticks, but only out of a fight: when
        /// nothing hurt it for <see cref="BattleSettings.HealAfterTicks"/> (task 4.7).
        /// </summary>
        private void Heal()
        {
            for (var i = 0; i < _state.Heroes.Count; i++)
            {
                var hero = _state.Heroes[i];
                var full = _settings.HeroHealthOf(hero.Side);
                if (!hero.Alive || hero.Health >= full || _state.Tick - hero.HurtTick < _settings.HealAfterTicks)
                    continue;

                var cell = _state.Grid.At(hero.X / BattleSettings.UnitsPerCell, hero.Y / BattleSettings.UnitsPerCell);
                if (cell != hero.Side)
                    continue;

                hero.RegenAccumulator += _settings.HeroRegenPerSecond;
                while (hero.RegenAccumulator >= _settings.TicksPerSecond)
                {
                    hero.RegenAccumulator -= _settings.TicksPerSecond;
                    hero.Health++;
                }

                if (hero.Health > full)
                    hero.Health = full;
                _state.Heroes[i] = hero;
            }
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

                var distance = BattleState.Distance2(hero.X, hero.Y, BattleState.Middle(site.CenterX), BattleState.Middle(site.CenterY));
                if (distance <= reach2 && distance < bestDistance)
                {
                    best = s;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static bool OnSite(HeroState hero, SiteState site)
        {
            const int unit = BattleSettings.UnitsPerCell;
            var x0 = (site.CenterX - 1) * unit;
            var y0 = (site.CenterY - 1) * unit;
            return hero.X >= x0 && hero.X < x0 + 3 * unit && hero.Y >= y0 && hero.Y < y0 + 3 * unit;
        }

        private void ForAura(SiteState site, System.Action<int> visit)
        {
            var size = _state.Grid.Size;
            var radius = _settings.AuraCells;
            for (var y = site.CenterY - radius; y <= site.CenterY + radius; y++)
            for (var x = site.CenterX - radius; x <= site.CenterX + radius; x++)
            {
                var dx = x - site.CenterX;
                var dy = y - site.CenterY;
                if (x >= 0 && y >= 0 && x < size && y < size && dx * dx + dy * dy <= radius * radius)
                    visit(y * size + x);
            }
        }

        private void Add(ResourceKind kind, int cell, int amount)
        {
            var size = _state.Grid.Size;
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

        private bool NearSomeone(int cell)
        {
            var size = _state.Grid.Size;
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
