using UnityEngine;

namespace Core.InputManagement.Interfaces
{
    /// <summary>
    /// Where the player wants the hero to go: a finger or the held mouse as a floating stick, or WASD and the arrows.
    /// </summary>
    public interface IMoveInput
    {
        /// <summary>
        /// The direction on the screen, length 1, or zero: the hero stands
        /// </summary>
        Vector2 Direction { get; }

        /// <summary>
        /// A finger or the mouse holds the stick now
        /// </summary>
        bool StickShown { get; }

        /// <summary>
        /// The centre of the base, canvas units from the lower left corner of the screen
        /// </summary>
        Vector2 StickCenter { get; }

        /// <summary>
        /// The knob from the centre of the base, canvas units, at most the travel
        /// </summary>
        Vector2 KnobOffset { get; }

        /// <summary>
        /// Off while results or a pause are shown: the input is dropped and the stick hidden
        /// </summary>
        bool Enabled { get; set; }
    }
}
