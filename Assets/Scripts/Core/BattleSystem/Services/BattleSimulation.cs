using System.Collections.Generic;
using Core.BattleSystem.Structs;
using Core.BattleSystem.Utils;

namespace Core.BattleSystem.Services
{
    /// <summary>
    /// The battle: integers only, one call of <see cref="Step"/> is one tick, commands wait for their tick. The same settings
    /// and commands give the same <see cref="ComputeHash"/> at every tick on any device and frame rate.
    /// The rules here are a placeholder that exercises determinism; the game rules are still open questions (CLAUDE.md).
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly BattleSettings _settings;
        private readonly ZoneState[] _zones;
        private readonly List<WaveState> _waves = new List<WaveState>();
        private readonly List<BattleCommand> _commands = new List<BattleCommand>();
        private DeterministicRandom _random;

        public BattleSimulation(BattleSettings settings)
        {
            _settings = settings;
            _random = new DeterministicRandom(settings.Seed);
            _zones = new ZoneState[settings.ZoneCount];
            for (var i = 0; i < _zones.Length; i++)
                _zones[i] = new ZoneState { Owner = i % settings.FactionCount, Troops = settings.StartTroops };
        }

        /// <summary>The number of ticks already run.</summary>
        public int Tick { get; private set; }

        public IReadOnlyList<ZoneState> Zones => _zones;

        public IReadOnlyList<WaveState> Waves => _waves;

        /// <summary>Queues a command for a future tick; a command for a past tick is refused, or two runs could differ.</summary>
        public bool Enqueue(BattleCommand command)
        {
            if (command.Tick <= Tick)
                return false;

            var index = _commands.Count;
            while (index > 0 && _commands[index - 1].Tick > command.Tick)
                index--;

            _commands.Insert(index, command);
            return true;
        }

        public void Step()
        {
            Tick++;
            ApplyCommands();
            MoveWaves();
            Produce();
        }

        /// <summary>FNV-1a of the whole state: two runs are the same exactly when their hashes match at every tick.</summary>
        public ulong ComputeHash()
        {
            var hash = 14695981039346656037UL;
            hash = Mix(hash, Tick);
            hash = Mix(hash, (int)_random.State);
            for (var i = 0; i < _zones.Length; i++)
            {
                hash = Mix(hash, _zones[i].Owner);
                hash = Mix(hash, _zones[i].Troops);
            }

            for (var i = 0; i < _waves.Count; i++)
            {
                hash = Mix(hash, _waves[i].Owner);
                hash = Mix(hash, _waves[i].ToZone);
                hash = Mix(hash, _waves[i].Troops);
                hash = Mix(hash, _waves[i].ArrivalTick);
            }

            return hash;
        }

        private void ApplyCommands()
        {
            while (_commands.Count > 0 && _commands[0].Tick == Tick)
            {
                var command = _commands[0];
                _commands.RemoveAt(0);
                SendWave(command);
            }
        }

        private void SendWave(BattleCommand command)
        {
            ref var from = ref _zones[command.FromZone];
            var troops = from.Troops / 2;
            if (from.Owner != command.Faction || troops == 0 || command.FromZone == command.ToZone)
                return;

            from.Troops -= troops;
            _waves.Add(new WaveState
            {
                Owner = command.Faction,
                ToZone = command.ToZone,
                Troops = troops,
                ArrivalTick = Tick + _settings.WaveTravelTicks + _random.Next(_settings.WaveTravelJitterTicks),
            });
        }

        private void MoveWaves()
        {
            for (var i = 0; i < _waves.Count; i++)
            {
                if (_waves[i].ArrivalTick > Tick)
                    continue;

                Land(_waves[i]);
                _waves.RemoveAt(i);
                i--;
            }
        }

        private void Land(WaveState wave)
        {
            ref var zone = ref _zones[wave.ToZone];
            if (zone.Owner == wave.Owner)
            {
                zone.Troops += wave.Troops;
                return;
            }

            zone.Troops -= wave.Troops;
            if (zone.Troops >= 0)
                return;

            zone.Owner = wave.Owner;
            zone.Troops = -zone.Troops;
        }

        private void Produce()
        {
            if (Tick % _settings.ProductionPeriodTicks != 0)
                return;

            for (var i = 0; i < _zones.Length; i++)
                _zones[i].Troops++;
        }

        private static ulong Mix(ulong hash, int value)
        {
            const ulong prime = 1099511628211UL;
            var bits = (uint)value;
            for (var i = 0; i < 4; i++)
            {
                hash ^= (bits >> (i * 8)) & 0xFF;
                hash *= prime;
            }

            return hash;
        }
    }
}
