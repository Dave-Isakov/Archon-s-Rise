using UnityEngine;

// A reactive one-shot tip (M2.12): fired at most once per profile by its real
// trigger, only while tutoring is enabled, and only after the rail is done
// (deferred otherwise, shown oldest-first after the send-off).
[CreateAssetMenu(fileName = "OneShotTip", menuName = "ArchonsRise/Tutorial/One-Shot Tip")]
public class TutorialOneShotSO : ScriptableObject
{
    [Tooltip("Stable id — the tut.oneshot.<id> PlayerPrefs key. Never rename after ship.")]
    public string id;
    [TextArea(2, 5)] public string bannerText;
    [Tooltip("TutorialTarget id to highlight; empty = no highlight.")]
    public string highlightTargetId;
    [Tooltip("Event id (plan's event-id contract) that triggers this tip.")]
    public string triggerEventId;
}
