using UnityEngine;
using UnityEngine.EventSystems;

// Relays mouse hover into the pop-out's focus model so hover and gamepad focus
// share one highlight. One per focusable element (segments, empower, play, back).
public class InspectorFocusHover : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] InspectorNavController controller;
    [SerializeField] InspectorSection section;
    [SerializeField] StatSegment segment; // set for Choice/Improvise elements; null otherwise
    [SerializeField] bool isBack;         // distinguishes Back from Play in the Play section

    public void OnPointerEnter(PointerEventData eventData)
        => controller.FocusFromHover(section, segment, isBack);
}
