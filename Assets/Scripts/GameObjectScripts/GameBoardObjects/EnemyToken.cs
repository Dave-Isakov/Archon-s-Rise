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
    // Doom scaling applied at spawn time. Lives on the token — the shared
    // EnemiesSO asset is NEVER mutated.
    public int bonusHP;
    public int bonusAttack;
    // Mid-run spawns are saved explicitly (schema v4); only map-gen tokens
    // use the seed-derived defeatedEnemies cell mechanism.
    public bool isMidRunSpawn;
    // Shrine-guardian binding (spec 2026-07-24, §3): -1 = an ordinary spawn.
    // Otherwise the (int)ShrineReward this guardian owes at 2x on defeat, plus
    // the originating shrine's cell to mark it consumed, and the ShrineSO the
    // payout draws its pools from.
    public int shrineRewardType = -1;
    public Vector3Int shrineCell;
    public ShrineSO shrineSO;
    public Vector3Int gridPos;
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

    public void CheckAggro(PlayerPosition player)
    {
        foreach(Directions direction in Enum.GetValues(typeof(Directions)))
        {
            if((gridPos + compass[direction]) == gameboard.LocalToCell(player.transform.position) && !isAggro)
            {
                this.isAggro = true;
                break;
            }
            else if((gridPos + compass[direction]) == gameboard.LocalToCell(player.transform.position) && isAggro)
            {
                player.inCombat = true;
                StartCoroutine(StartCombat());
                break;
            }
        }

        if(gridPos + compass[Directions.Northwest] != gameboard.LocalToCell(player.transform.position)
        && gridPos + compass[Directions.Northeast] != gameboard.LocalToCell(player.transform.position)
        && gridPos + compass[Directions.East] != gameboard.LocalToCell(player.transform.position)
        && gridPos + compass[Directions.West] != gameboard.LocalToCell(player.transform.position)
        && gridPos + compass[Directions.Southwest] != gameboard.LocalToCell(player.transform.position)
        && gridPos + compass[Directions.Southeast] != gameboard.LocalToCell(player.transform.position))
            this.isAggro = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // Out-of-combat-range tokens are not clickable (spec 2026-07-24 follow-up).
        // The old else-branch opened a preview card on the combat canvas, but its
        // only dismissal was a bare-card click that the card's buttons absorb —
        // stranding the player with an un-closable screen. Enemy preview is
        // deferred to a hover affordance; only an in-range (aggro) token engages.
        if (!isAggro) return;

        StartCoroutine(StartCombat());
    }

    IEnumerator StartCombat()
    {
        // Opening is a free look (spec 2026-07-30 §2.3): still gated on the
        // turn's action being available at all (no point opening a fight you
        // can't commit to), but BeginAction() itself waits for a real commit.
        if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanInteract)
        {
            GameLog.Instance.Post("You've already taken your action this turn.");
            yield break;
        }

        GameManager.Instance.activeCombatant = this;
        yield return GameManager.Instance.PlayCombatIntro();
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(enemy, bonusHP, bonusAttack)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Field, fieldToken: this,
            onCommit: () => { if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.BeginAction(); });
    }
}
