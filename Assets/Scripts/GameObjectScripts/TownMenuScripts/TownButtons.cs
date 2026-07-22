using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class TownButtons : MonoBehaviour
{
    [SerializeField] protected TownToken _town;
    [SerializeField] protected TownEvent townEvent;
    [SerializeField] protected IntEvent influenceCostEvent;
    [SerializeField] protected Button thisButton;
    [SerializeField] protected TextMeshProUGUI buttonText;
    [SerializeField] protected int currentPlayerInfluence;

    // The one locked/unaffordable look (UiLock, alpha 0.4). Wired per button in
    // the editor; null-safe so unwired buttons keep their current appearance.
    [SerializeField] protected CanvasGroup lockGroup;

    protected void SyncLock() => UiLock.Apply(lockGroup, !thisButton.interactable);

    // A service may only be committed when the current visit still owns the turn's
    // action (spec 2026-07-22). Null-safe so buttons behave normally with no
    // controller in the scene (tests / isolated harnesses).
    protected static bool CanActThisVisit =>
        TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;

    protected void Awake()
    {
        
    }
    public void SetTownCard(TownToken town)
    {
        this._town = town;
    }

    // Read-only access for the preview trigger sharing this GameObject.
    public TownToken Town => _town;

    public void SetCurrentInfluence(int influence)
    {
        currentPlayerInfluence = influence;
    }

    public abstract void UpdateButtonText();
}
