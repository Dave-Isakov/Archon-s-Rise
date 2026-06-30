using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The Play button (label = live preview) plus Back. Play is disabled for unplayable
// cards (Wounds). Back closes the inspector.
public class PlayBar : MonoBehaviour
{
    [SerializeField] CardInspector inspector;
    [SerializeField] Button playButton;
    [SerializeField] TextMeshProUGUI playLabel;
    [SerializeField] Button backButton;

    void OnEnable()  { inspector.Changed += Render; }
    void OnDisable() { inspector.Changed -= Render; }

    void Start()
    {
        playButton.onClick.AddListener(() => inspector.Play());
        backButton.onClick.AddListener(() => inspector.Close());
    }

    void Render()
    {
        var sel = inspector.Selection;
        if (sel == null) return;
        bool playable = sel.IsPlayable();
        playButton.interactable = playable;
        playLabel.text = playable ? $"PLAY · {sel.Describe()}" : "Cannot play";
        playLabel.color = playable ? StatPalette.For(sel.ResolvedStat()) : StatPalette.Muted;
    }
}
