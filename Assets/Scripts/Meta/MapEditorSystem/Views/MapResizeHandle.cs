using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Meta.MapEditorSystem.Views
{
    /// <summary>
    /// A handle on the frame of the map in the editor (owner, 5 November: the bounds he pulls are the map): on an edge or a corner,
    /// its <see cref="Edge"/> says which — x −1 the west edge, +1 the east one, y −1 the south, +1 the north, 0 neither. Drags go to
    /// <see cref="MapEditorView"/>.
    /// </summary>
    public sealed class MapResizeHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Vector2Int edge;

        public Vector2Int Edge => edge;

        public event Action<MapResizeHandle, PointerEventData> Began;
        public event Action<MapResizeHandle, PointerEventData> Dragged;
        public event Action<MapResizeHandle, PointerEventData> Ended;

        // a press on a handle is not a stroke of the brush on the map under it
        public void OnPointerDown(PointerEventData eventData) { }

        public void OnBeginDrag(PointerEventData eventData) => Began?.Invoke(this, eventData);

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(this, eventData);

        public void OnEndDrag(PointerEventData eventData) => Ended?.Invoke(this, eventData);

#if UNITY_EDITOR
        public void SetEdgeForEditor(Vector2Int value) => edge = value;
#endif
    }
}
