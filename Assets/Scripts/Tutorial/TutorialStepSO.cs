using UnityEngine;

// Which edge the instruction banner sits against. Authored per step so the banner
// can get out of the way of whatever that step is asking the player to look at —
// the play-card step points at the Improvise buttons, which the left-hand pose
// covers. Left is 0 so every already-authored asset keeps its current placement.
public enum BannerSide
{
    Left = 0,
    Right = 1,
}

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
    [Tooltip("Which edge the banner anchors to for this step. Flip it to Right when " +
             "the left-hand pose would cover what the step is pointing at.")]
    public BannerSide bannerSide = BannerSide.Left;
}
