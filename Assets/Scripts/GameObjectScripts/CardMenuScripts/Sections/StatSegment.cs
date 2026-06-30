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

    public void SetState(State state)
    {
        Color accent = StatPalette.For(stat);
        switch (state)
        {
            case State.Selected:
                background.color = accent;
                label.color = new Color(0.05f, 0.07f, 0.09f, 1f); // dark text on bright fill
                button.interactable = true;
                if (glow != null) glow.SetActive(true);
                break;

            case State.Available:
                background.color = new Color(accent.r, accent.g, accent.b, 0.14f);
                label.color = StatPalette.Muted;
                button.interactable = true;
                if (glow != null) glow.SetActive(false);
                break;

            case State.Locked:
                background.color = new Color(StatPalette.Locked.r, StatPalette.Locked.g, StatPalette.Locked.b, 0.40f);
                label.color = StatPalette.Locked;
                button.interactable = false;
                if (glow != null) glow.SetActive(false);
                break;
        }
    }
}
