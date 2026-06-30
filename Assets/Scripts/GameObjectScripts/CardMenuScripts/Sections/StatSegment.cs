using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One Choice/Improvise option button. Renders Selected / Available / Locked from
// StatPalette so selection reads as colour + glow, never as a greyed-out button.
public class StatSegment : MonoBehaviour
{
    public enum State { Selected, Available, Locked }

    [SerializeField] StatType stat;
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Button button;
    [SerializeField] GameObject glow;          // optional outer-glow object, lit only when Selected

    public StatType Stat => stat;
    public Button Button => button;

    // glow is an optional separate child object. Guard against it being mis-wired to the
    // segment's own GameObject: SetActive(false) on self would deactivate the whole
    // segment (and silently hide any available-but-unselected choice).
    void SetGlow(bool on) { if (glow != null && glow != gameObject) glow.SetActive(on); }

    public void SetState(State state)
    {
        Color accent = StatPalette.For(stat);
        switch (state)
        {
            case State.Selected:
                background.color = accent;
                label.color = new Color(0.05f, 0.07f, 0.09f, 1f); // dark text on bright fill
                button.interactable = true;
                SetGlow(true);
                break;

            case State.Available:
                background.color = new Color(accent.r, accent.g, accent.b, 0.14f);
                label.color = StatPalette.Muted;
                button.interactable = true;
                SetGlow(false);
                break;

            case State.Locked:
                background.color = new Color(StatPalette.Locked.r, StatPalette.Locked.g, StatPalette.Locked.b, 0.40f);
                label.color = StatPalette.Locked;
                button.interactable = false;
                SetGlow(false);
                break;
        }
    }
}
