using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArchonsRise.Shrines;

public enum CombatContext { Field, Guardian, Dungeon }

// Owns one phased fight (spec 2026-07-21, Spec 2): the CombatPhase machine, the
// logical live-enemy set, the per-fight context, and the single multi-purpose
// button. Engage/kill/withdraw are added in Tasks 6-8.
public class CombatController : MonoBehaviour
{
    public static CombatController Instance { get; private set; }

    [SerializeField] VoidEvent onCombatPhaseChanged; // HUD phase label listens

    [SerializeField] EnemyTraitTuningSO traitTuning;   // wired in Task 11 step 5

    [Header("Enemy placement — ring (guardian / multi, spec 2026-07-27)")]
    [SerializeField] float ringRadius = 200f;              // px out from the ring centre
    [SerializeField] Vector2 ringNudge = new Vector2(0f, 40f); // ring centre, relative to the player
    [SerializeField] float slotSpacing = CombatLayoutRules.SlotSpacingDegrees; // deg between neighbours

    [Header("Enemy placement — map token (field / dungeon)")]
    [SerializeField] float tokenFanRadius = 110f;             // px out, if a token fight ever has 2+
    [SerializeField, Range(0f, 1f)] float centreBias = 0.5f;  // 0 = at the token, 1 = at the player
    [SerializeField] Vector2 tokenOffset = new Vector2(0f, 40f); // screen-px nudge off the icon

    [Header("Enemy placement — card + keep-outs")]
    [SerializeField] float baseCardScale = 1.2f;                    // card size on the board
    [SerializeField] Vector2 cardSize = new Vector2(125f, 170f);    // art footprint at scale 1
    [SerializeField] float screenMargin = 12f;                      // min gap from the left/right/top edges
    [SerializeField] float handKeepOut = 200f;                      // px of screen bottom the hand owns
    // Player-relative box the parked Advance/Withdraw buttons occupy. A token
    // cluster that would land on them is pushed clear; the ring is authored to
    // clear them already, so it ignores this.
    [SerializeField] Rect buttonKeepOut = new Rect(40f, -120f, 300f, 240f);

    // Where the fan is centred and how far out it reaches, in the parent rect's
    // local space — which the anchor normalisation below makes "pixels from the
    // player". Cached at fight-open; the layout runs once and never re-fans.
    Vector2 layoutCentre;
    float layoutRadius;
    // The source token's true projected point: where cards fly out of and back
    // to, unclamped, so the morph still reads as "that icon became this card".
    Vector2 morphSource;

    public CombatPhase Phase { get; private set; }
    public bool CanSiege        => CombatPhaseRules.CanSiege(Phase);
    public bool CanInfluence    => CombatPhaseRules.CanInfluence(Phase);
    public bool CanNormalAttack => CombatPhaseRules.CanNormalAttack(Phase);
    // True from the final kill until the canvas actually closes: input is gated
    // (Phase == Resolved) but the death FX is still playing and the canvas is open.
    bool resolving;

    // A fight is live in any non-Resolved phase, or while the closing FX plays.
    // Phase is initialized to Resolved in Awake so this reads false before the
    // first fight (the enum's default is Siege, which would otherwise report
    // combat when none is running).
    public bool InCombat => Phase != CombatPhase.Resolved || resolving;

    readonly List<EnemyCard> live = new();   // logical set; resolution keys off THIS, not childCount
    CombatContext context;
    TownToken guardianPlace;   // Guardian: the assaulted place
    EnemyToken fieldToken;     // Field: the map token, destroyed + save-recorded on defeat
    DungeonToken dungeonToken; // Dungeon: depth/completion tracked on defeat

    // Captured (enemy name, reward) pairs for killed enemies; the exp/crystal is
    // banked immediately by CaptureReward, but the naming message + card pick are
    // paid at fight-end so a kill mid-fight never pops a modal that interrupts
    // Siege/Attack decisions. RewardSummary carries no name, so we pair it here.
    readonly List<(string name, RewardSummary summary)> pendingRewards = new();

    // Shrine-guardian binding (spec 2026-07-24, §3). Captured in NotifyDefeated
    // — RecordFieldDefeat destroys the token, so the owed reward must be read
    // off it first — and paid in FinishEnd, after the ordinary defeat rewards,
    // so the shrine's card pick queues behind the "you defeated X" message
    // rather than interrupting it. -1 = this fight owes no shrine reward.
    int pendingShrineType = -1;
    Vector3Int pendingShrineCell;
    ShrineSO pendingShrineSo;

    public bool HasLiveEnemies => live.Count > 0;

    // Live-set readouts for the advance-button controller (spec 2026-07-24 phase
    // controls): the counterattack total the Defend preview compares against, and
    // a "can this staged siege kill anyone" test for the Siege-phase hide. The
    // buttons are parked in the scene now, so nothing reads the cluster centroid.
    public int LiveEnemyAttackTotal
    {
        get { int t = 0; foreach (var c in live) if (c != null) t += c.EffectiveAttack; return t; }
    }

    public bool AnySiegeKillable(int siege)
    {
        foreach (var c in live) if (c != null && c.EffectiveHP <= siege) return true;
        return false;
    }

    // Lights every live enemy's Siege/Influence buttons (spec 2026-07-24 phase
    // controls). Driven by the advance-button controller off the staged pools;
    // keeps the live set encapsulated.
    public void SetEnemyActionGlow(bool siegeLit, bool influenceLit)
    {
        foreach (var c in live) if (c != null) c.SetActionGlow(siegeLit, influenceLit);
    }

    // The pure roster every rule consumes. Rebuilt on demand so it always
    // reflects current kills and blocks.
    // Public: the preview panel (Task 14) needs both to show roster-aware values.
    public List<EnemyCombatant> Roster()
    {
        var list = new List<EnemyCombatant>();
        foreach (var card in live) list.Add(card.ToCombatant());
        return list;
    }

    public EnemyTraitTuning Tuning =>
        traitTuning != null ? traitTuning.tuning : new EnemyTraitTuning();

    public CounterattackPreview Preview() => EnemyTraitRules.BuildPreview(Roster(), Tuning);

    // Blocks last one Defend phase only.
    public void ClearBlocks()
    {
        foreach (var card in live) card.Blocked = false;
    }

    public struct EnemySpawn
    {
        public EnemiesSO so; public int bonusHP; public int bonusAttack;
        public EnemySpawn(EnemiesSO so, int bonusHP, int bonusAttack)
        { this.so = so; this.bonusHP = bonusHP; this.bonusAttack = bonusAttack; }
    }

    void Awake() { Instance = this; Phase = CombatPhase.Resolved; }

    // Opens a phased fight. The source varies by context — guardianPlace for a
    // Guardian assault, fieldToken for a Field encounter, dungeonToken for a
    // Dungeon delve — and drives that context's win bookkeeping (Task 9).
    public void OpenFight(List<EnemySpawn> spawns, CombatContext context,
        TownToken guardianPlace = null, EnemyToken fieldToken = null, DungeonToken dungeonToken = null)
    {
        this.context = context;
        this.guardianPlace = guardianPlace;
        this.fieldToken = fieldToken;
        this.dungeonToken = dungeonToken;
        live.Clear();
        pendingShrineType = -1;   // a new fight inherits no owed shrine reward
        pendingShrineSo = null;

        var parent = GameManager.Instance.enemyCardCombatPosition.transform;
        // Clear any stragglers (a fled fight's survivors, or an out-of-range peek
        // card) so a new fight never inherits stale cards.
        foreach (var stale in parent.GetComponentsInChildren<EnemyCard>())
            Destroy(stale.gameObject);

        var prefab = FindAnyObjectByType<EnemyDeck>().PrefabEnemyCard;
        foreach (var s in spawns)
        {
            var go = Instantiate(prefab, parent);
            var card = go.GetComponent<EnemyCard>();
            card.enemySO = s.so;
            card.bonusHP = s.bonusHP;
            card.bonusAttack = s.bonusAttack;
            Normalize((RectTransform)card.transform);
            live.Add(card);
        }

        morphSource = OriginLocalPoint(OriginWorld());
        PlaceFight();
        foreach (var card in live)
        {
            var morph = card.GetComponent<EnemyCardMorph>();
            if (morph != null) morph.MorphIn(morphSource); // fly out from the board spot to the slot
        }
        if (context == CombatContext.Field && fieldToken != null)
            fieldToken.SetBoardVisible(false);

        GameManager.Instance.combatCanvas.enabled = true;
        SetPhase(CombatPhase.Siege); // CombatButtons (spec 2026-07-24) owns the phase controls now
    }

    void SetPhase(CombatPhase phase)
    {
        Phase = phase;
        foreach (var card in live) card.ApplyPhase(phase);
        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }

    // Centre-anchors a spawned card so its anchoredPosition means "pixels from
    // the player". The prefab root is authored anchored AND pivoted at the
    // bottom-left corner, which made every slot depend on the parent rect's size
    // (spec 2026-07-27); normalising here keeps the fix in one place instead of
    // relying on prefab hygiene.
    static void Normalize(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
    }

    // Picks the fan's centre + radius for this fight, lays the cards out once,
    // then slides the whole cluster clear of the screen edges, the hand, and the
    // parked buttons (spec 2026-07-27). A guardian assault — or any fight with a
    // roster — rings the player; a lone enemy from a map token appears over its
    // icon instead, so the card still reads as "that thing over there".
    void PlaceFight()
    {
        bool ring = context == CombatContext.Guardian || live.Count >= 2;
        layoutCentre = ring ? PlayerLocal() + ringNudge : TokenCentreLocal();
        layoutRadius = ring ? ringRadius : tokenFanRadius;
        LayoutLive();
        ApplyClusterSafety(pushOffButtons: !ring); // the ring is authored to clear the buttons
    }

    // Positions the live set in an even fan around layoutCentre. Called ONCE, at
    // fight-open: a card's slot is fixed for the fight. Re-fanning after a kill
    // made the survivors slide and resize around the gap, which read as jittery
    // in multi-guardian fights (feedback 2026-07-27). Presentation only.
    void LayoutLive()
    {
        int n = live.Count;
        for (int i = 0; i < n; i++)
            ApplySlot(live[i], CombatLayoutRules.SpacedSlotFor(i, n, layoutRadius, slotSpacing));
    }

    void ApplySlot(EnemyCard card, CombatLayoutRules.Slot slot)
    {
        var rt = (RectTransform)card.transform;
        rt.anchoredPosition = layoutCentre + new Vector2(slot.X, slot.Y);
        float s = baseCardScale * slot.Scale;
        rt.localScale = new Vector3(s, s, 1f);
        rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltDeg);
    }

    // The token fan's centre: the icon pulled part-way toward the player (the
    // band between the two always reads clearly) and nudged up, mirroring how
    // EnemyPreviewPanel seats the hover preview. Screen px in, local px out.
    Vector2 TokenCentreLocal()
    {
        Vector2 token = RectTransformUtility.WorldToScreenPoint(Camera.main, OriginWorld());
        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        return ScreenToLocal(Vector2.Lerp(token, centre, centreBias) + tokenOffset);
    }

    // Translates the whole cluster — rigidly, so the fan's shape survives — until
    // it is inside the safe area and, on the token path, off the parked buttons.
    // The keep-out push can shove the cluster back out of the safe area, so the
    // clamp runs again after it; one extra pass settles every case where the
    // keep-out is smaller than the safe area, which it always is.
    void ApplyClusterSafety(bool pushOffButtons)
    {
        if (live.Count == 0) return;

        Shift(CombatLayoutRules.ShiftIntoSafeArea(ClusterBox(), SafeArea()));
        if (!pushOffButtons) return;

        var keepOut = new CombatLayoutRules.Box(
            buttonKeepOut.xMin, buttonKeepOut.yMin, buttonKeepOut.xMax, buttonKeepOut.yMax);
        Shift(CombatLayoutRules.ShiftOutOfKeepOut(ClusterBox(), keepOut));
        Shift(CombatLayoutRules.ShiftIntoSafeArea(ClusterBox(), SafeArea()));
    }

    void Shift(CombatLayoutRules.Anchor delta)
    {
        if (delta.X == 0f && delta.Y == 0f) return;
        var d = new Vector2(delta.X, delta.Y);
        layoutCentre += d;
        foreach (var card in live)
            if (card != null) ((RectTransform)card.transform).anchoredPosition += d;
    }

    // Union of every live card's footprint. The prefab root rect is zero-sized
    // (the art lives on children), so the footprint comes from the authored
    // cardSize scaled by what the slot applied.
    CombatLayoutRules.Box ClusterBox()
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var card in live)
        {
            if (card == null) continue;
            var rt = (RectTransform)card.transform;
            Vector2 half = cardSize * rt.localScale.x * 0.5f;
            Vector2 p = rt.anchoredPosition;
            if (p.x - half.x < minX) minX = p.x - half.x;
            if (p.y - half.y < minY) minY = p.y - half.y;
            if (p.x + half.x > maxX) maxX = p.x + half.x;
            if (p.y + half.y > maxY) maxY = p.y + half.y;
        }
        return new CombatLayoutRules.Box(minX, minY, maxX, maxY);
    }

    // The canvas, inset by the edge margins, with the hand's band carved off the
    // bottom — expressed in the cards' local space.
    CombatLayoutRules.Box SafeArea()
    {
        var canvasRect = (RectTransform)GameManager.Instance.combatCanvas.rootCanvas.transform;
        Vector2 c = PlayerLocal();
        float halfW = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;
        return new CombatLayoutRules.Box(c.x - halfW + screenMargin, c.y - halfH + handKeepOut,
                                         c.x + halfW - screenMargin, c.y + halfH - screenMargin);
    }

    // The player, in the cards' local space. The camera rides PlayerPosition, so
    // the player is the canvas centre; this expresses that centre relative to the
    // CombatPosition parent, which means the parent can be parked anywhere in the
    // canvas without dragging the ring or the safe area along with it.
    Vector2 PlayerLocal()
    {
        var canvasRect = (RectTransform)GameManager.Instance.combatCanvas.rootCanvas.transform;
        var parent = (RectTransform)GameManager.Instance.enemyCardCombatPosition.transform;
        return -(Vector2)canvasRect.InverseTransformPoint(parent.position);
    }

    // Screen px -> the parent combat rect's local px (which Normalize makes the
    // same space a card's anchoredPosition lives in).
    Vector2 ScreenToLocal(Vector2 screen)
    {
        var canvas = GameManager.Instance.combatCanvas;
        var parent = (RectTransform)GameManager.Instance.enemyCardCombatPosition.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, canvas.worldCamera, out Vector2 local);
        return local;
    }

    // World position the cards emerge from / return to, per fight context.
    Vector3 OriginWorld()
    {
        if (context == CombatContext.Field && fieldToken != null) return fieldToken.transform.position;
        if (context == CombatContext.Guardian && guardianPlace != null) return guardianPlace.transform.position;
        if (context == CombatContext.Dungeon && dungeonToken != null) return dungeonToken.transform.position;
        return GameManager.Instance.enemyCardCombatPosition.transform.position; // fallback: centre
    }

    // Project a board world position into the cards' local UI space, so a card
    // can fly from its token to its slot. Board camera -> screen -> canvas.
    Vector2 OriginLocalPoint(Vector3 worldPos)
        => ScreenToLocal(RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos));

    // Engage (Siege -> Defend, spec 2026-07-22): commit the Siege-phase removals
    // and open the Defend window. Siege is a Siege-phase-only currency, cleared
    // here. NO counterattack yet — that waits for the Defend press so the player
    // can play defense first.
    public void Engage()
    {
        if (Phase != CombatPhase.Siege) return;

        var player = FindAnyObjectByType<Player>();
        player.PlayerSiege = 0;                       // Siege doesn't carry past Engage
        GameManager.Instance.commands.ClearStack();   // Engage is a commit point

        SetPhase(CombatPhase.Defend);
    }

    // Defend (Defend -> Attack, spec 2026-07-22): resolve the summed survivor
    // counterattack against whatever Defend the player built during the window —
    // one HP-bite comparison, wounds for the shortfall — then open the Attack phase.
    public void ResolveDefend()
    {
        if (Phase != CombatPhase.Defend) return;
        var player = FindAnyObjectByType<Player>();

        var preview = Preview();
        int defendLeft = player.PlayerDefend;
        int toughness = player.PlayerToughness;

        int handWounds    = EnemyTraitRules.HandWounds(preview, defendLeft, toughness);
        int discardWounds = EnemyTraitRules.DiscardWounds(preview, defendLeft, toughness, Tuning);
        int stolen        = EnemyTraitRules.CrystalsStolen(preview, defendLeft, toughness, Tuning);

        // The placement list is the seam (spec §6.2): today a pure rule produces
        // it, next phase an interactive picker does. This consumer depends only
        // on IReadOnlyList<WoundDestination> and never learns what a unit is.
        var placements = WoundPlacementRules.Place(handWounds, discardWounds);
        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        foreach (var dest in placements) hand.AddWound(dest);

        int wounds = placements.Count;

        // Taking the group counterattack reads on the avatar (spec D4).
        if (wounds > 0 && PlayerAvatar.Instance != null)
            PlayerAvatar.Instance.Play(AvatarState.Hurt);

        player.PlayerDefend = Mathf.Max(0, player.PlayerDefend - preview.UnblockedThreat);
        GameManager.Instance.commands.ClearStack();   // taking the hit is a commit point

        if (wounds > 0)
            GameLog.Instance.Post($"The enemies strike back! You are wounded {wounds} times.");

        if (stolen > 0)
        {
            var crystals = FindAnyObjectByType<CrystalInventory>();
            if (crystals != null)
            {
                int n = Mathf.Min(stolen, crystals.crystalsInInventory.Count);
                for (int i = 0; i < n; i++) crystals.crystalsInInventory[0].RemoveCrystal();
            }
        }
        ClearBlocks();

        SetPhase(CombatPhase.Attack);
    }

    // Called when a specific enemy is removed (Siege/Attack kill, or Influence).
    // Banks the kill immediately; the FX plays out and self-destroys the card.
    public void NotifyDefeated(EnemyCard card, bool wasInfluence)
    {
        if (!live.Remove(card)) return;

        // No re-layout: survivors hold the slots they opened the fight in.

        // Attack/Siege kills swing; Influence removals are the fade-and-drift
        // track and play no attack animation (spec D4).
        if (!wasInfluence && PlayerAvatar.Instance != null)
            PlayerAvatar.Instance.Play(AvatarState.Fight);

        GameManager.Instance.commands.ClearStack();   // a kill is irreversible

        // Shrine-guardian binding (spec 2026-07-24, §3): read the owed reward off
        // the token BEFORE RecordFieldDefeat tears it down. An Influence removal
        // counts too — the guardian is gone either way, and leaving the shrine
        // Guarding with nothing to fight would strand its reward forever.
        if (context == CombatContext.Field && fieldToken != null && fieldToken.shrineRewardType >= 0)
        {
            pendingShrineType = fieldToken.shrineRewardType;
            pendingShrineCell = fieldToken.shrineCell;
            pendingShrineSo = fieldToken.shrineSO;
        }

        // Per-context win bookkeeping, banked at kill time (parallels how the
        // guardian ConquestTracker record used to fire from GuardianAssault.Update).
        if (context == CombatContext.Guardian && guardianPlace != null)
            ConquestTracker.Instance.RecordDefeat(guardianPlace.gridPos);
        else if (context == CombatContext.Field && fieldToken != null)
            RecordFieldDefeat(fieldToken);
        else if (context == CombatContext.Dungeon && dungeonToken != null)
            RecordDungeonDefeat(dungeonToken);

        // Exp/crystal bank now; the name + card pick are paid at fight-end. Dungeon
        // fights are exp-only (spec 2026-07-13), driven by context, not a flag. A
        // shrine guardian is exp-only too (spec 2026-07-24, §3): it drops nothing
        // beyond its defeat exp — the shrine's doubled reward IS its loot.
        pendingRewards.Add((card.enemySO.cardName,
            GameManager.Instance.CaptureReward(card,
                expOnly: pendingShrineType >= 0 || context == CombatContext.Dungeon)));

        // Attack-phase kills are otherwise wound-free at point of use; Vengeful
        // is the one exception. Siege and Influence kills stay clean. Phase is
        // still Attack here (wasLast hasn't flipped it to Resolved yet), and
        // Siege-phase kills never reach this branch since Siege only runs while
        // Phase == Siege.
        if (!wasInfluence && Phase == CombatPhase.Attack)
        {
            var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
            int vengeful = EnemyTraitRules.VengefulWounds(card.ToCombatant(), Roster(), Tuning);
            for (int i = 0; i < vengeful; i++) hand.AddWound(WoundDestination.Hand);
            if (vengeful > 0)
                GameLog.Instance.Post($"It strikes as it falls — you take {vengeful} wound(s).");
        }

        // On the final kill, gate further input (Resolved) but keep the canvas
        // open; the fight only closes once the death FX finishes (spec 2026-07-22),
        // so the player actually sees the dissolve/fade.
        bool wasLast = !HasLiveEnemies;
        if (wasLast) { Phase = CombatPhase.Resolved; resolving = true; }
        System.Action onFxDone = wasLast ? (System.Action)(() => EndFight(paidFlee: false)) : null;

        var fx = card.GetComponent<EnemyCardDefeatFx>();
        if (wasInfluence) fx.PlayFade(onFxDone); else fx.PlayDestroy(onFxDone);
    }

    // A field enemy's map token is removed and its cell recorded so a map-gen
    // enemy never respawns on reload (mid-run spawns aren't cell-tracked).
    void RecordFieldDefeat(EnemyToken token)
    {
        if (!token.isMidRunSpawn && DataManager.Instance != null)
            DataManager.Instance.DefeatedEnemies.Add(
                new ArchonsRise.SaveData.Cell(token.gridPos.x, token.gridPos.y));
        Destroy(token.gameObject);
    }

    // A delve win records depth, refreshes the token's marker, and completes the
    // dungeon when the last slot falls (mirrors the old DungeonDelve.Update).
    void RecordDungeonDefeat(DungeonToken token)
    {
        DungeonTracker.Instance.RecordDefeat(token.gridPos);
        token.RefreshVisual();
        if (DungeonTracker.Instance.IsComplete(token.gridPos))
            DungeonTracker.Instance.CompleteDungeon(token);
    }

    // The multi-purpose button in the Attack phase. Survivors alive => this IS
    // the flee (field/dungeon 1 wound, guardian 3-wound retreat). Kills banked.
    public void Withdraw()
    {
        if (Phase != CombatPhase.Attack) return;

        // Capture the roster BEFORE EndFight/FinishEnd clears `live` — Harrying
        // needs the fleeing enemies' traits, not the empty post-flee state.
        var rosterBeforeFlee = Roster();

        var hand = GameManager.Instance.playerHand.GetComponent<PlayerHand>();
        int cost = context == CombatContext.Guardian ? PlaceRules.RetreatWoundCount : 1;
        for (int i = 0; i < cost; i++) hand.AddWound();

        GameLog.Instance.Post(context == CombatContext.Guardian
            ? $"You retreat from the assault and suffer {cost} wounds. Your progress is kept."
            : "You flee the battle and suffer a wound!");

        int harry = EnemyTraitRules.HarryPenalty(rosterBeforeFlee, Tuning);
        if (harry > 0)
        {
            var player = FindAnyObjectByType<Player>();
            player.PendingHandPenalty += harry;
            GameLog.Instance.Post($"They harry your retreat — you draw {harry} fewer card(s) next turn.");
        }

        EndFight(paidFlee: true);
    }

    // Fight-end payout + close. Every captured reward is paid through the queue in
    // kill order (deferred so mid-fight kills never interrupt decisions). On a
    // cleared guardian roster we fire the conquest message + victory check.
    void EndFight(bool paidFlee)
    {
        Phase = CombatPhase.Resolved;
        resolving = false;

        // A flee leaves survivors: morph them back to the board first, then finish.
        // (A cleared fight reaches EndFight from the last kill's FX with live empty.)
        if (paidFlee && live.Count > 0)
        {
            StartCoroutine(MorphSurvivorsBackThenFinish());
            return;
        }
        FinishEnd(paidFlee);
    }

    IEnumerator MorphSurvivorsBackThenFinish()
    {
        Vector2 toLocal = OriginLocalPoint(OriginWorld());
        var survivors = new List<EnemyCard>(live);
        int pending = 0;
        foreach (var card in survivors)
            if (card != null && card.GetComponent<EnemyCardMorph>() != null) pending++;

        foreach (var card in survivors)
        {
            if (card == null) continue;
            var morph = card.GetComponent<EnemyCardMorph>();
            if (morph != null) morph.MorphBack(toLocal, () => pending--);
        }
        if (pending > 0) yield return new WaitUntil(() => pending <= 0);

        if (context == CombatContext.Field && fieldToken != null)
            fieldToken.SetBoardVisible(true);
        FinishEnd(paidFlee: true);
    }

    void FinishEnd(bool paidFlee)
    {
        // Destroy any remaining cards (fled survivors; killed cards self-destruct).
        foreach (var card in live)
            if (card != null) Destroy(card.gameObject);
        live.Clear();

        foreach (var (name, summary) in pendingRewards)
        {
            var n = name; var s = summary;   // capture per-iteration for the closure
            RewardQueue.Instance.Enqueue(done => { GameManager.Instance.PayReward(n, s); done(); });
        }
        pendingRewards.Clear();

        // The shrine's owed payout, at 2x (spec 2026-07-24, §3), queued behind the
        // ordinary defeat rewards so its card pick never pre-empts the defeat
        // message. Paying it retires the shrine: Guarding -> ConsumedDormant.
        if (pendingShrineType >= 0 && pendingShrineSo != null)
        {
            var rewards = FindAnyObjectByType<Rewards>();
            if (rewards != null)
                rewards.GrantShrineReward((ShrineReward)pendingShrineType,
                    ShrineRules.RewardCount(false), pendingShrineSo);
            ShrineTracker.Instance.SetState(pendingShrineCell, ShrineVisualState.ConsumedDormant);
        }
        pendingShrineType = -1;
        pendingShrineSo = null;

        if (!paidFlee && context == CombatContext.Guardian && guardianPlace != null
            && ConquestTracker.Instance.IsConquered(guardianPlace.gridPos))
        {
            GameLog.Instance.Post(
                $"{guardianPlace.townSO.cardName} is conquered! Its services are now open to you.");
            if (RunEndRules.IsVictory(ConquestTracker.Instance.ConqueredCastleCount()))
                RunEndController.RequestEnd(RunOutcome.Victory);
        }

        // Fleeing a field fight leaves the token on the map; de-aggro it so the
        // player must step away and back to re-engage (parity with the old Flee).
        if (paidFlee && context == CombatContext.Field && fieldToken != null)
        {
            fieldToken.isAggro = false;
            if (fieldToken.player != null) fieldToken.player.inCombat = false;
        }

        GameManager.Instance.CloseCombatCanvas();
        guardianPlace = null;
        fieldToken = null;
        dungeonToken = null;

        // Resolved: the shared phase label falls back to the turn phase (Action,
        // since a fight is the turn's action) — see PhaseHud.OnCombatPhaseChanged.
        if (onCombatPhaseChanged != null) onCombatPhaseChanged.Raise();
    }
}
