using System.Collections.Generic;
using UnityEngine;

// Opens a guarded-place assault as one phased multi-enemy fight (spec 2026-07-21,
// Spec 2): the WHOLE remaining roster spawns at once. Per-kill banking +
// 3-wound retreat (both in CombatController) preserve resumable conquest.
public class GuardianAssault : MonoBehaviour
{
    private static GuardianAssault instance;
    public static GuardianAssault Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("GuardianAssault").AddComponent<GuardianAssault>();
            return instance;
        }
    }

    public void Begin(TownToken town)
    {
        // Assaulting is the visit's committed action (spec 2026-07-22): spend the
        // turn's action now (the AssaultButton is gated so this only fires when the
        // visit still owns it).
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();

        // Tear down the place menu the button click came from.
        foreach (var card in FindObjectsByType<TownCard>())
            Destroy(card.gameObject);
        GameManager.Instance.townCanvas.enabled = false;
        GameManager.Instance.CombatCanvasActive(); // canvas chrome + multi-purpose button, no field banner

        var roster = town.townSO.guardians;
        int already = ConquestTracker.Instance.DefeatedCount(town.gridPos);
        var spawns = new List<CombatController.EnemySpawn>();
        for (int i = already; i < roster.Count; i++)
            spawns.Add(new CombatController.EnemySpawn(roster[i], 0, 0)); // guardians unscaled

        CombatController.Instance.OpenFight(spawns, CombatContext.Guardian, town);
    }
}
