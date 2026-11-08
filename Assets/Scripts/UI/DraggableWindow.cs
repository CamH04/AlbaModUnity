using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Moves a RectTransform window by dragging its title bar.
/// Attach this to the title bar and assign the window root to <see cref="target"/>.
/// </summary>
public class DraggableWindow : MonoBehaviour, IBeginDragHandler, IDragHandler {
    [Tooltip("The window RectTransform that should move.")]
    public RectTransform target;

    private Vector2 _dragOffset;

    void Awake() {
        if (target == null)
            target = transform.parent as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData) {
        if (target == null) return;

        var parent = target.parent as RectTransform;
        if (parent == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out var local))
            _dragOffset = target.anchoredPosition - local;
    }

    public void OnDrag(PointerEventData eventData) {
        if (target == null) return;

        var parent = target.parent as RectTransform;
        if (parent == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, eventData.pressEventCamera, out var local))
            target.anchoredPosition = local + _dragOffset;
    }
}
