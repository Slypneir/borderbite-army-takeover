using System;
using Core.BattleSystem.Services;
using Core.BattleSystem.Structs;

namespace Meta.BattleSystem.Utils
{
    /// <summary>The place a view has read the events of a battle up to; a new battle starts it after the events it already has.</summary>
    public struct EventCursor
    {
        private BattleSimulation _simulation;
        private int _read;

        /// <summary>Hands every event since the last call to <paramref name="handle"/>; none on the first call of a battle.</summary>
        public void Read(BattleSimulation simulation, Action<BattleEvent> handle)
        {
            if (simulation != _simulation)
            {
                _simulation = simulation;
                _read = simulation?.Events.Count ?? 0;
                return;
            }

            if (simulation == null)
                return;

            var events = simulation.Events;
            for (; _read < events.Count; _read++)
                handle(events[_read]);
        }
    }
}
