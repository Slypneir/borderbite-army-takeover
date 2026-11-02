using System;

namespace Core.PlatformSystem.Interfaces
{
    /// <summary>
    /// Everything that may stop the game or its sound in one place: the pause of the platform, a lost focus, an advert, the mute.
    /// </summary>
    public interface IPlaybackGate
    {
        /// <summary>
        /// The game must not run
        /// </summary>
        bool Frozen { get; }

        /// <summary>
        /// The game must be silent
        /// </summary>
        bool Muted { get; }

        /// <summary>
        /// <see cref="Frozen"/> or <see cref="Muted"/> changed
        /// </summary>
        event Action OnChanged;

        /// <summary>
        /// An advert was requested: the game waits from the request to the answer, the sound stops only when the advert plays
        /// </summary>
        void SetAdRequested(bool requested);
    }
}
