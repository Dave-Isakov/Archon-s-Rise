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

    [Header("Withdraw (last resort)")]
    [SerializeField] Button withdrawButton;

    [Header("State colours")]
    [SerializeField] Color engageColor = Color.white;
    [SerializeField] Color takeHitColor = new Color(0.90f, 0.20f, 0.20f);       // red
    [SerializeField] Color counterattackColor = new Color(0.30f, 0.80f, 0.35f); // strike-back

    [Header("Idle sway (owned here so nothing fights the parked position)")]
    [SerializeField] float swayPosAmplitude = 4f;    // px
    [SerializeField] float swayTiltAmplitude = 1.5f; // degrees
    [SerializeField] float swayPeriod = 3.2f;        // seconds per cycle

    Player player;
    // Where each button was authored in the scene. Both are parked by hand now
    // (spec 2026-07-27): anchoring them opposite the enemy cluster made them jump
    // on every kill and left the enemy layout dodging a moving target. The sway
    // plays around these, so the script never redefines where a button lives.
    Vector2 advanceHome, withdrawHome;

    void Start()
    {
        if (advanceButton != null)
        {
            advanceButton.onClick.AddListener(OnAdvance);
            advanceHome = ((RectTransform)advanceButton.transform).anchoredPosition;
        }
        if (withdrawButton != null)
        {
            withdrawButton.onClick.AddListener(OnWithdraw);
            withdrawHome = ((RectTransform)withdrawButton.transform).anchoredPosition;
        }
    }

    void Update()
    {
        var cc = CombatController.Instance;
        if (cc == null || !cc.InCombat) { HideAll(); return; }
        if (player == null) player = FindAnyObjectByType<Player>();
        if (player == null) { HideAll(); return; }

        var st = CombatPhaseRules.Advance(cc.Phase, player.PlayerSiege, cc.AnySiegeKillable(player.PlayerSiege),
            player.PlayerDefend, cc.Preview(), player.PlayerToughness, canCommit: !cc.PreviewOnly);

        RenderAdvance(st);
        RenderWithdraw(cc);

        // Nothing invites a spend in a preview-only fight, so the Siege/Influence
        // glows stay dark alongside the buttons they belong to.
        bool siege = cc.Phase == CombatPhase.Siege && !cc.PreviewOnly;
        cc.SetEnemyActionGlow(siege && player.PlayerSiege > 0, siege && player.PlayerInfluence > 0);
    }

    void RenderAdvance(AdvanceState st)
    {
        bool show = st.Kind != AdvanceKind.Hidden;
        if (advanceButton != null) advanceButton.gameObject.SetActive(show);
        if (!show) return;

        PlaceWithSway((RectTransform)advanceButton.transform, advanceHome);

        string text; Color color;
        if (st.Kind == AdvanceKind.Engage) { text = "Engage"; color = engageColor; }
        else if (st.Kind == AdvanceKind.Counterattack) { text = "-> Counterattack!"; color = counterattackColor; }
        else { text = $"{st.Wounds} {IconMarkup.Tag(IconConcept.Wound)} - use {IconMarkup.Tag(IconConcept.Defend)}!!"; color = takeHitColor; }

        if (advanceLabel != null) { advanceLabel.text = text; advanceLabel.color = color; }
        if (advanceGlow != null) advanceGlow.color = color;
    }

    void RenderWithdraw(CombatController cc)
    {
        bool show = cc.Phase == CombatPhase.Attack;
        if (withdrawButton != null) withdrawButton.gameObject.SetActive(show);
        if (!show) return;

        PlaceWithSway((RectTransform)withdrawButton.transform, withdrawHome);
    }

    // Nudges a button around its authored home with a subtle idle sway. Owning
    // the write here (rather than a separate sway component) keeps one writer of
    // the RectTransform, so nothing can drift the button off its parked spot.
    void PlaceWithSway(RectTransform rt, Vector2 home)
    {
        float w = Time.time / Mathf.Max(0.01f, swayPeriod) * Mathf.PI * 2f;
        Vector2 sway = new Vector2(Mathf.Sin(w) * swayPosAmplitude,
                                   Mathf.Cos(w * 0.5f) * swayPosAmplitude * 0.5f);
        rt.anchoredPosition = home + sway;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(w) * swayTiltAmplitude);
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
}
