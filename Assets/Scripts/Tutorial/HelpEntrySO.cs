using UnityEngine;

// One panel's ? explainer (M2.12): what this screen is, what you can do, what
// it costs — short, with registry icons inline.
[CreateAssetMenu(fileName = "HelpEntry", menuName = "ArchonsRise/Tutorial/Help Entry")]
public class HelpEntrySO : ScriptableObject
{
    [Tooltip("Stable id — the tut.help.<panelId> PlayerPrefs key. Never rename after ship.")]
    public string panelId;
    public string title;
    [TextArea(3, 10)] public string body;
}
