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
        // Tear down the place menu the button click came from.
        foreach (var card in FindObjectsByType<TownCard>())
            Destroy(card.gameObject);
        GameManager.Instance.townCanvas.enabled = false;
        GameManager.Instance.CombatCanvasActive(); // canvas chrome + multi-purpose button, no field banner

        // Resume by identity, not by count: the player can kill any guardian
        // first (Siege reaches slot 1 as easily as slot 0), so a resumed assault
        // re-spawns exactly the slots still standing.
        var roster = town.townSO.guardians;
        var spawns = new List<CombatController.EnemySpawn>();
        foreach (int i in ConquestTracker.Instance.RemainingIndices(town.gridPos, roster.Count))
            spawns.Add(new CombatController.EnemySpawn(roster[i], 0, 0, i)); // guardians unscaled

        // Opening the assault is now a free look (spec 2026-07-30 §2.3);
        // assaulting is still the visit's committed action, but only once the
        // player actually commits inside the fight.
        CombatController.Instance.OpenFight(spawns, CombatContext.Guardian, town,
            onCommit: () => { if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction(); });
    }
}
