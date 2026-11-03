using Core.AudioManagement.Enums;

namespace Core.AudioManagement.Interfaces
{
    /// <summary>
    /// Plays one-shot sounds at the volume of their row in the table of sounds.
    /// </summary>
    public interface ISfxPlayer
    {
        /// <summary>
        /// Plays a sound once; silent while the game is muted (the gate pauses the listener)
        /// </summary>
        void Play(Sfx sound);
    }
}
