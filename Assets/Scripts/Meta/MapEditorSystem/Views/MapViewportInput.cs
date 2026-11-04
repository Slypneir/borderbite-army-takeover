using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Meta.MapEditorSystem.Views
{
    /// <summary>The pointer over the map of the editor: presses, drags, releases and the wheel, handed to <see cref="MapEditorView"/>.</summary>
    public sealed class MapViewportInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler,
        IPointerMoveHandler, IBeginDragHandler
    {
        public event Action<PointerEventData> Pressed;
        public event Action<PointerEventData> Dragged;
        public event Action<PointerEventData> Released;
        public event Action<PointerEventData> Scrolled;
        public event Action<PointerEventData> Moved;

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke(eventData);

        // without it a drag starts only past the threshold of the event system: the first cells of a stroke would be lost
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

        public void OnPointerUp(PointerEventData eventData) => Released?.Invoke(eventData);

        public void OnScroll(PointerEventData eventData) => Scrolled?.Invoke(eventData);

        public void OnPointerMove(PointerEventData eventData) => Moved?.Invoke(eventData);
    }
}
