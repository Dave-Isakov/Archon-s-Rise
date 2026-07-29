using System.Collections.Generic;
using TMPro;
using UnityEngine;

// One enemy's stat block inside the preview panel. Renders name / Attack / HP /
// Influence-cost using the same sprite-tag format as EnemyCard so the preview
// matches the combat card. Instantiated once per previewed enemy. No art field
// exists on the data model, so there is no art element here.
public class EnemyPreviewEntry : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI enemyName;
    [SerializeField] TextMeshProUGUI enemyAttack;
    [SerializeField] TextMeshProUGUI enemyHP;
    [SerializeField] TextMeshProUGUI enemyInfluence;
    // Trait legend (Task 14, spec §8.3): badge + name + generated rule per trait,
    // one line each. Null-safe like EnemyCard.traitBadges — unwired prefabs keep
    // showing the plain stat block until this field is hooked up in the editor.
    [SerializeField] TextMeshProUGUI traitLines;

    // `index`/`roster`/`tuning` let this entry read the SAME roster-aware trait
    // math CombatController uses in the real fight (Task 11/14): a Warlord or
    // Outrider elsewhere in `roster` changes THIS entry's effective Attack, and
    // showing the raw SO value here would be a lie the player only discovers
    // after committing (spec 2026-07-29 §4).
    public void Populate(EnemyPreviewData data, int index, IReadOnlyList<EnemyCombatant> roster, EnemyTraitTuning tuning)
    {
        enemyName.text = data.enemy.cardName;
        // Attack shows the roster-effective Threat (spec §4: BaseAttack + Warlord
        // aura, Swift-multiplied) — the number Defend will actually be measured
        // against, not the enemy's bare authored Attack.
        int threat = EnemyTraitRules.Threat(index, roster, tuning);
        enemyAttack.text = IconMarkup.Tag(IconConcept.Attack) + " \n" + threat.ToString();
        enemyHP.text = IconMarkup.Tag(IconConcept.Hp) + " \n" + (data.enemy.enemyHP + data.bonusHP).ToString();
        if (data.enemy.canInfluence)
        {
            enemyInfluence.gameObject.SetActive(true);
            enemyInfluence.text = IconMarkup.Tag(IconConcept.Influence) + " \n" + data.enemy.influenceCost.ToString();
        }
        else
        {
            enemyInfluence.gameObject.SetActive(false);
        }

        if (traitLines != null)
        {
            var builder = new System.Text.StringBuilder();
            // The preview is the LEGEND for the card's bare badges (spec §8.3):
            // badge + name + generated rule. A badge is never shown anywhere the
            // player cannot reach a preview. Uses the card's own authored traits
            // (not the granted/effective mask) so the legend lists exactly the
            // badges EnemyCard.RefreshTraitBadges puts on the live card — a
            // granting trait (Warlord/Miasma/Ironclad/Outrider) narrates its
            // effect on the rest of the roster through its own Rule() text
            // instead of duplicating a badge onto every other entry.
            foreach (var t in EnemyTraitCopy.Split(data.enemy.traits))
                builder.AppendLine(IconMarkup.TraitBadgeTinted(t) + " " +
                                   IconMarkup.TraitName(t) + " — " +
                                   EnemyTraitCopy.Rule(t, tuning));
            traitLines.text = builder.ToString();
            traitLines.gameObject.SetActive(builder.Length > 0);
        }
    }
}
