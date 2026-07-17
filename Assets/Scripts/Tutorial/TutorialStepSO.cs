using UnityEngine;

// One rail step (M2.12). The manager runs an ordered list of these; a step
// completes on its event id (see the plan's event-id contract) or, when the
// id is empty, on the banner's Next button. Copy is authored inline with
// registry sprite tags — no localization.
[CreateAssetMenu(fileName = "RailStep", menuName = "ArchonsRise/Tutorial/Rail Step")]
public class TutorialStepSO : ScriptableObject
{
    [Tooltip("Stable id — persistence key component. Never rename after ship.")]
    public string id;
    [TextArea(2, 5)] public string bannerText;
    [Tooltip("TutorialTarget id to highlight; empty = no highlight.")]
    public string highlightTargetId;
    [Tooltip("Event id that completes this step; empty = informational (Next button).")]
    public string completionEventId;
}
