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
        /// The player's pause screen is open (stage 1 spec 5.4)
        /// </summary>
        bool PlayerPaused { get; }

        /// <summary>
        /// The player's pause screen opens (true) or closes (false): it stops the game and its sound until closed
        /// </summary>
        void SetPlayerPaused(bool paused);

        /// <summary>
        /// An advert was requested: the game waits from the request to the answer, the sound stops only when the advert plays
        /// </summary>
        void SetAdRequested(bool requested);
    }
}
