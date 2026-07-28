using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// The one dismiss gesture (spec 2026-07-28): a full-screen transparent Image
// behind a surface's content. Clicking anywhere that is not content closes it.
//
// MUST sit at sibling index 0 within its surface so it renders BEHIND the
// content — at any other index it swallows the clicks it is meant to sit under.
//
// Two arming models, both supported with no code change:
//   * A surface that toggles its Canvas.enabled needs no arming at all — a
//     disabled canvas raycasts nothing, so the catcher dies with the surface.
//   * A surface that stays active and only hides its contents (the crystal
//     pop-out) calls SetArmed. The GameObject stays active while disarmed, so
//     any event listeners on it keep receiving events — the pattern the retired
//     CrystalDismissCatcher used.
[RequireComponent(typeof(Image))]
public class ClickOffCatcher : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Invoked when the player clicks off the surface. Wire the surface's close method here.")]
    public UnityEvent onClickOff;

    [Tooltip("Force the Image fully transparent on Awake. Turn OFF only when the " +
             "catcher doubles as a deliberate dimming scrim.")]
    [SerializeField] bool forceTransparent = true;

    Image image;

    void Awake()
    {
        image = GetComponent<Image>();
        // A fully transparent Image still receives raycasts, which is exactly
        // what we want: invisible, but clickable. raycastTarget is independent
        // of alpha, so this cannot accidentally disarm the catcher.
        if (forceTransparent)
        {
            var c = image.color;
            c.a = 0f;
            image.color = c;
        }
    }

    // Arm when the surface opens, disarm when it closes. Only needed by surfaces
    // that stay active while closed; see the class comment.
    public void SetArmed(bool armed)
    {
        if (image == null) image = GetComponent<Image>();
        image.raycastTarget = armed;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (image != null && !image.raycastTarget) return;
        if (onClickOff != null) onClickOff.Invoke();
    }
}
