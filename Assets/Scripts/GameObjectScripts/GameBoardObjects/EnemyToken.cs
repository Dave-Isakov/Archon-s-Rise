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
    [SerializeField] SpriteRenderer glow; // soft halo child, pulses while the player is adjacent
    // Doom scaling applied at spawn time. Lives on the token — the shared
    // EnemiesSO asset is NEVER mutated.
    public int bonusHP;
    public int bonusAttack;
    // Mid-run spawns are saved explicitly (schema v4); only map-gen tokens
    // use the seed-derived defeatedEnemies cell mechanism.
    public bool isMidRunSpawn;
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

    private EnemyDeck deck;
    void Start()
    {
        gridPos = gameboard.LocalToCell(transform.position);
        player = FindAnyObjectByType<PlayerPosition>();
        deck = FindAnyObjectByType<EnemyDeck>();
        player.UpdateCompass(gridPos, compass);
    }

    void Update()
    {
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

        if(this.isAggro)
        {
            StartCoroutine(StartCombat());
        }
        else
        {
            GameManager.Instance.combatCanvas.enabled = true;
            deck.GetNewEnemyCard(this);
        }
    }

    IEnumerator StartCombat()
    {
        // A fight is the turn's one action (spec 2026-07-21): block a second
        // encounter, and taking this one performs the implicit Explore->Action
        // transition (commits the movement stack, locks further movement).
        if (TurnPhaseController.Instance != null)
        {
            if (!TurnPhaseController.Instance.CanInteract)
            {
                GameManager.Instance.ValidationMessage("You've already taken your action this turn.");
                yield break;
            }
            TurnPhaseController.Instance.BeginAction();
        }

        GameManager.Instance.activeCombatant = this;
        yield return GameManager.Instance.PlayCombatIntro();
        var spawns = new List<CombatController.EnemySpawn>
        {
            new CombatController.EnemySpawn(enemy, bonusHP, bonusAttack)
        };
        CombatController.Instance.OpenFight(spawns, CombatContext.Field, fieldToken: this);
    }
}
