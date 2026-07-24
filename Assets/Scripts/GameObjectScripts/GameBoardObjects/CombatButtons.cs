using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Phase-aware combat controls (spec 2026-07-24). Replaces the old always-on
// multi-purpose button with two purpose-built controls:
//   • Advance — the Siege/Defend commit. Anchors opposite the enemy cluster near
//     the heart of combat, hides while staged siege can be spent, and previews
//     the incoming counterattack live during Defend.
//   • Withdraw — a separate, muted, edge-pushed control shown only in Attack, so
//     resolving the counterattack can never land on an accidental flee.
// Presentation + input only: presses dispatch to the controller's existing
// Engage/ResolveDefend/Withdraw; all combat math lives in the pure rules.
public class CombatButtons : MonoBehaviour
{
    [Header("Advance (Siege/Defend commit)")]
    [SerializeField] Button advanceButton;
    [SerializeField] TextMeshProUGUI advanceLabel;
    [SerializeField] Image advanceGlow;               // optional glow plate (UIPulseGlow.mat); tinted per state
    [SerializeField] float advanceDistance = 220f;    // px opposite the enemy centroid

    [Header("Withdraw (last resort)")]
    [SerializeField] Button withdrawButton;
    [SerializeField] float withdrawDistance = 460f;   // px, further out toward the edge

    [Header("State colours")]
    [SerializeField] Color engageColor = Color.white;
    [SerializeField] Color takeHitColor = new Color(0.90f, 0.20f, 0.20f);       // red
    [SerializeField] Color counterattackColor = new Color(0.30f, 0.80f, 0.35f); // strike-back

    [Header("On-screen clamp (px half-extents from centre)")]
    [SerializeField] Vector2 clampHalfExtents = new Vector2(360f, 260f);

    Player player;

    void Start()
    {
        if (advanceButton != null) advanceButton.onClick.AddListener(OnAdvance);
        if (withdrawButton != null) withdrawButton.onClick.AddListener(OnWithdraw);
    }

    void Update()
    {
        var cc = CombatController.Instance;
        if (cc == null || !cc.InCombat) { HideAll(); return; }
        if (player == null) player = FindAnyObjectByType<Player>();
        if (player == null) { HideAll(); return; }

        var st = CombatPhaseRules.Advance(cc.Phase, player.PlayerSiege, cc.AnySiegeKillable(player.PlayerSiege),
            player.PlayerDefend, cc.LiveEnemyAttackTotal, player.PlayerToughness);

        RenderAdvance(st, cc);
        RenderWithdraw(cc);

        bool siege = cc.Phase == CombatPhase.Siege;
        cc.SetEnemyActionGlow(siege && player.PlayerSiege > 0, siege && player.PlayerInfluence > 0);
    }

    void RenderAdvance(AdvanceState st, CombatController cc)
    {
        bool show = st.Kind != AdvanceKind.Hidden;
        if (advanceButton != null) advanceButton.gameObject.SetActive(show);
        if (!show) return;

        var a = CombatLayoutRules.OppositeAnchor(cc.EnemyCentroidLocal.x, cc.EnemyCentroidLocal.y, advanceDistance);
        ((RectTransform)advanceButton.transform).anchoredPosition = Clamp(new Vector2(a.X, a.Y));

        string text; Color color;
        if (st.Kind == AdvanceKind.Engage) { text = "Engage"; color = engageColor; }
        else if (st.Kind == AdvanceKind.Counterattack) { text = "-> Counterattack!"; color = counterattackColor; }
        else { text = $"Gain {st.Wounds} {IconMarkup.Tag(IconConcept.Wound)} - use {IconMarkup.Tag(IconConcept.Defend)}!!"; color = takeHitColor; }

        if (advanceLabel != null) { advanceLabel.text = text; advanceLabel.color = color; }
        if (advanceGlow != null) advanceGlow.color = color;
    }

    void RenderWithdraw(CombatController cc)
    {
        bool show = cc.Phase == CombatPhase.Attack;
        if (withdrawButton != null) withdrawButton.gameObject.SetActive(show);
        if (!show) return;

        var a = CombatLayoutRules.OppositeAnchor(cc.EnemyCentroidLocal.x, cc.EnemyCentroidLocal.y, withdrawDistance);
        ((RectTransform)withdrawButton.transform).anchoredPosition = Clamp(new Vector2(a.X, a.Y));
    }

    // The advance press means different things per phase; only ever visible in
    // Siege (Engage) or Defend (resolve the counterattack).
    void OnAdvance()
    {
        var cc = CombatController.Instance;
        if (cc == null) return;
        if (cc.Phase == CombatPhase.Siege) cc.Engage();
        else if (cc.Phase == CombatPhase.Defend) cc.ResolveDefend();
    }

    void OnWithdraw() { if (CombatController.Instance != null) CombatController.Instance.Withdraw(); }

    void HideAll()
    {
        if (advanceButton != null) advanceButton.gameObject.SetActive(false);
        if (withdrawButton != null) withdrawButton.gameObject.SetActive(false);
    }

    Vector2 Clamp(Vector2 p) => new Vector2(
        Mathf.Clamp(p.x, -clampHalfExtents.x, clampHalfExtents.x),
        Mathf.Clamp(p.y, -clampHalfExtents.y, clampHalfExtents.y));
}
