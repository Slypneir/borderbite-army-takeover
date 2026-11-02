using System;
using Core.ScreenSystem.Enums;

namespace Core.ScreenSystem.Interfaces
{
    /// <summary>
    /// The shape of the window now; the camera already follows it.
    /// </summary>
    public interface IScreenLayout
    {
        /// <summary>
        /// Portrait or landscape
        /// </summary>
        ScreenShape Shape { get; }

        /// <summary>
        /// The window changed its size or shape
        /// </summary>
        event Action OnChanged;
    }
}
