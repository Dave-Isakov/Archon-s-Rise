using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One Choice/Improvise option button. Renders Selected / Available / Locked from
// StatPalette so selection reads as colour + glow, never as a greyed-out button.
public class StatSegment : MonoBehaviour
{
    public enum State { Selected, Available, Locked }

    // EXPERIMENT: which glow visual lights on Selected. Sprite = the original static
    // glow sprite (unchanged). Shader = the UIPulseGlow breathing halo. Defaults to
    // Sprite so behaviour is unchanged until flipped per-segment in the Inspector.
    public enum GlowMode { Sprite, Shader }

    [SerializeField] StatType stat;
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Button button;
    [SerializeField] GameObject glow;          // optional outer-glow object, lit only when Selected

    [Header("Shader-glow experiment")]
    [SerializeField] GlowMode glowMode = GlowMode.Sprite; // default preserves current look
    [SerializeField] GameObject shaderGlow;    // optional child Image w/ UIPulseGlow.mat, lit only when Selected & mode == Shader

    public StatType Stat => stat;
    public Button Button => button;

    // glow is an optional separate child object. Guard against it being mis-wired to the
    // segment's own GameObject: SetActive(false) on self would deactivate the whole
    // segment (and silently hide any available-but-unselected choice).
    void SetGlow(bool on) { if (glow != null && glow != gameObject) glow.SetActive(on); }

    // Same self-guard as SetGlow for the parallel shader-glow object.
    void SetShaderGlow(bool on) { if (shaderGlow != null && shaderGlow != gameObject) shaderGlow.SetActive(on); }

    public void SetState(State state)
    {
        Color accent = StatPalette.For(stat);
        switch (state)
        {
            case State.Selected:
                background.color = accent;
                label.color = new Color(0.05f, 0.07f, 0.09f, 1f); // dark text on bright fill
                button.interactable = true;
                if (glowMode == GlowMode.Shader)
                {
                    SetGlow(false);
                    SetShaderGlow(true);
                    // Drive per-segment glow colour through the Image's vertex colour so
                    // one shared UIPulseGlow material tints to each stat's accent.
                    if (shaderGlow != null && shaderGlow != gameObject)
                    {
                        var img = shaderGlow.GetComponent<Image>();
                        if (img != null) img.color = accent;
                    }
                }
                else
                {
                    SetShaderGlow(false);
                    SetGlow(true);
                }
                break;

            case State.Available:
                background.color = new Color(accent.r, accent.g, accent.b, 0.14f);
                label.color = StatPalette.Muted;
                button.interactable = true;
                SetGlow(false);
                SetShaderGlow(false);
                break;

            case State.Locked:
                background.color = new Color(StatPalette.Locked.r, StatPalette.Locked.g, StatPalette.Locked.b, 0.40f);
                label.color = StatPalette.Locked;
                button.interactable = false;
                SetGlow(false);
                SetShaderGlow(false);
                break;
        }
    }
}
