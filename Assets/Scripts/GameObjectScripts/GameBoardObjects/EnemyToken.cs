using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnemyToken : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Grid gameboard;
    public PlayerPosition player;
    public EnemiesSO enemy;
    public EnemyCard cardRef;
    public bool isAggro;
    public bool inCombat;
    bool boardHidden; // true while this token's card is up in combat
    [SerializeField] SpriteRenderer glow; // soft halo child, pulses while the player is adjacent
    // World size the enemy's own art is fitted to on the board. 1 reproduced the
    // placeholder's footprint exactly, but card art carries transparent margin the
    // placeholder didn't, so fitting to 1 left several enemies reading small in
    // their hex — hence the overshoot. Per-token dial in the Inspector.
    [SerializeField] float iconWorldSize = 1.35f;
    // Doom scaling applied at spawn time. Lives on the token — the shared
    // EnemiesSO asset is NEVER mutated.
    public int bonusHP;
    public int bonusAttack;
    // Mid-run spawns are saved explicitly (schema v4); only map-gen tokens
    // use the seed-derived defeatedEnemies cell mechanism.
    public bool isMidRunSpawn;
    // No shrine binding here any more (2026-07-31): a shrine guardian is never a
    // map token, so its owed reward lives on the shrine (ShrineLedger) and the
    // fight opens from the ShrineToken.
    public Vector3Int gridPos;
    // The hex the player stood on when this token armed. Stepping to a DIFFERENT
    // adjacent hex is what forces the fight. Deliberately not saved: a restored
    // save's default cell can only differ from wherever the player actually is,
    // which is exactly the "you already lingered here" reading we want.
    Vector3Int aggroCell;
    // A fight is being opened right now, by whichever token got there first. Static
    // because the race is BETWEEN tokens, not within one (see StartCombat).
    static bool opening;
    private Dictionary<Directions, Vector3Int> compass = new()
    {
        {Directions.Northwest, new Vector3Int(-1,1)},
        {Directions.Northeast, new Vector3Int(0,1)},
        {Directions.East, new Vector3Int(1,0)},
        {Directions.Southeast, new Vector3Int(0,-1)},
        {Directions.Southwest, new Vector3Int(-1,-1)},
        {Directions.West, new Vector3Int(-1,0)}
    };

    void Start()
    {
        gridPos = gameboard.LocalToCell(transform.position);
        player = FindAnyObjectByType<PlayerPosition>();
        player.UpdateCompass(gridPos, compass);
        ApplyArt();

        // A token can be born already inside the player's reach — the day's spawn
        // landing beside a player who is standing still. Nothing is going to move
        // to arm it, so it arms itself here (spec 2026-08-11). A save restore
        // overwrites this a frame later from the saved armed set, so a token the
        // player fled before saving stays inert (DataManager.RestoreAggro).
        RefreshAggro();
    }

    // Wears its own enemy's art on the board instead of the shared placeholder.
    // Uses EnemiesSO.cardArt — the same portrait the combat card shows — so the
    // icon the player walks up to is the thing that then fills the card.
    //
    // The renderer is the one on THIS object (the glow is the only child), so no
    // extra scene reference is involved. Every spawn path assigns `enemy` before
    // Start runs, whether from map gen, a mid-run spawn, or a save restore.
    void ApplyArt()
    {
        if (enemy == null || enemy.cardArt == null) return; // unauthored: keep the placeholder
        var icon = GetComponent<SpriteRenderer>();
        if (icon == null) return;

        icon.sprite = enemy.cardArt;
        // Card art is authored at portrait resolution, so its native world size is
        // nothing like a hex. Fit the LARGER axis to iconWorldSize and the token
        // can never swamp the cell it stands on, whatever the art's aspect or PPU.
        var size = enemy.cardArt.bounds.size;
        float largest = Mathf.Max(size.x, size.y);
        if (largest > 0f)
        {
            float s = iconWorldSize / largest;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    void Update()
    {
        if (boardHidden) return;

        // Field-defeat teardown (save-cell record + token destroy) now lives in
        // CombatController.RecordFieldDefeat (spec 2026-07-21, Spec 2), keyed off
        // the logical live set rather than this card's isDefeated flag.

        // Adjacency affordance: the halo pulses while the player stands next to
        // this token (isAggro) and the token isn't fog-hidden.
        if (glow != null)
        {
            bool show = isAggro && !MapFog.IsHidden(gridPos);
            if (show)
            {
                if (!glow.enabled) glow.enabled = true;
                var c = glow.color;
                c.a = GlowPulse.Alpha(Time.time, 0.3f, 1.0f, 4f);
                glow.color = c;
            }
            else if (glow.enabled)
            {
                glow.enabled = false;
            }
        }
    }

    // Hides/shows the board icon (and glow) while this token's combat card is up
    // (spec 2026-07-24). The card renders the enemy during the fight; a field
    // flee restores the icon.
    public void SetBoardVisible(bool visible)
    {
        boardHidden = !visible;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = visible;
    }

    // Raised for every token on every player move. Arms on the first step into
    // reach; the SECOND step — one adjacent hex to another adjacent hex — is the
    // player lingering in reach, and forces an inescapable fight (spec 2026-08-01).
    public void CheckAggro(PlayerPosition player)
    {
        Resolve(gameboard.LocalToCell(player.transform.position), AggroTrigger.PlayerMoved);
    }

    // Reach can change without the player taking a step: the fog lifting off this
    // token, or the token spawning beside them (spec 2026-08-11). Movement used to
    // be the only thing that ever ran this check, so an enemy that arrived either
    // way sat inert next to the player — no halo, and a click that did nothing.
    //
    // Both arm exactly like a step into reach, but neither can force a fight: the
    // player never chose to stand next to this enemy, so cornering them for it
    // would be unfair. Safe to call on a token that is already armed.
    public void RefreshAggro()
    {
        if (player == null || gameboard == null) return;
        Resolve(gameboard.LocalToCell(player.transform.position), AggroTrigger.ReachChanged);
    }

    void Resolve(Vector3Int playerCell, AggroTrigger trigger)
    {
        bool adjacent = false;
        foreach (Directions direction in Enum.GetValues(typeof(Directions)))
            if (gridPos + compass[direction] == playerCell) { adjacent = true; break; }

        // Comparing cells (rather than trusting the event to mean "moved") is what
        // makes a repeat raise at the player's current hex a no-op.
        var outcome = AggroRules.Resolve(
            fogHidden: MapFog.IsHidden(gridPos),
            adjacent: adjacent,
            wasArmed: isAggro,
            playerCellChanged: playerCell != aggroCell,
            trigger: trigger);

        isAggro = outcome.Armed;
        if (outcome.Armed) aggroCell = playerCell;
        if (outcome.Forced) StartCoroutine(StartCombat(forced: true));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable
        if (InputContextState.Current == InputContext.Map) return; // map mode: look, don't touch

        // Out-of-combat-range tokens are not clickable (spec 2026-07-24 follow-up).
        // The old else-branch opened a preview card on the combat canvas, but its
        // only dismissal was a bare-card click that the card's buttons absorb —
        // stranding the player with an un-closable screen. Enemy preview is
        // deferred to a hover affordance; only an in-range (aggro) token engages.
        if (!isAggro) return;

        StartCoroutine(StartCombat(forced: false));
    }

    // forced: the player was cornered rather than choosing this fight, so it opens
    // already committed and cannot be clicked off (spec 2026-08-01 §2.1).
    IEnumerator StartCombat(bool forced)
    {
        // Opening is a free look (spec 2026-07-30 §2.3): still gated on the
        // turn's action being available at all (no point opening a fight you
        // can't commit to), but BeginAction() itself waits for a real commit.
        // A forced fight is gated the same way — only a teleport card played in
        // the Action phase can reposition once the action is spent, and the token
        // simply stays armed for next turn.
        if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanInteract)
        {
            GameLog.Instance.Post("You've already taken your action this turn.");
            yield break;
        }

        // One fight at a time. Every armed token sees the same move, so a swarm
        // trigger runs this on two or three of them at once — and the intro beat
        // below means InCombat is still false while they queue up. The first one
        // through opens the fight; the rest are in its roster anyway.
        if (opening || (CombatController.Instance != null && CombatController.Instance.InCombat))
            yield break;
        opening = true;

        // Never a duel by default: everyone standing next to the player piles in.
        var participants = Participants();

        if (forced)
            GameLog.Instance.Post(participants.Count > 1
                ? $"Cornered! {participants.Count} enemies close in — there is no escape."
                : "Cornered! You cannot avoid this fight.");

        if (player != null) player.inCombat = true;
        GameManager.Instance.activeCombatant = this;
        yield return GameManager.Instance.PlayCombatIntro();

        var spawns = new List<CombatController.EnemySpawn>(participants.Count);
        foreach (var t in participants)
            spawns.Add(new CombatController.EnemySpawn(t.enemy, t.bonusHP, t.bonusAttack));

        CombatController.Instance.OpenFight(spawns, CombatContext.Field, fieldTokens: participants,
            onCommit: () => { if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.BeginAction(); },
            forced: forced);
        opening = false;   // the controller owns the fight from here (InCombat is true)
    }

    // This token plus every OTHER visible enemy standing next to the player
    // (spec 2026-08-01 §2.2). Board facts are gathered here; who actually joins is
    // FieldEncounterRules' call. Sorted by cell so the same board always builds the
    // same ring — FindObjectsByType promises no order.
    List<EnemyToken> Participants()
    {
        var all = new List<EnemyToken>(FindObjectsByType<EnemyToken>());
        all.Sort((a, b) => a.gridPos.x != b.gridPos.x
            ? a.gridPos.x.CompareTo(b.gridPos.x)
            : a.gridPos.y.CompareTo(b.gridPos.y));

        var neighbours = ExplorationController.Instance != null
            ? ExplorationController.Instance.PlayerNeighbors()
            : new Vector3Int[0];

        var adjacent = new List<bool>(all.Count);
        var hidden = new List<bool>(all.Count);
        foreach (var t in all)
        {
            adjacent.Add(Array.IndexOf(neighbours, t.gridPos) >= 0);
            hidden.Add(MapFog.IsHidden(t.gridPos));
        }

        var picked = FieldEncounterRules.Participants(all.IndexOf(this), adjacent, hidden);
        var tokens = new List<EnemyToken>(picked.Count);
        foreach (int i in picked) tokens.Add(all[i]);
        return tokens;
    }
}
