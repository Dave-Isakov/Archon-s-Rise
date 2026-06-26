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
        player = FindObjectOfType<PlayerPosition>();
        deck = FindObjectOfType<EnemyDeck>();
        player.UpdateCompass(gridPos, compass);
    }

    void Update()
    {
        if(cardRef is not null && cardRef.IsDefeated)
        {
            if (DataManager.Instance != null)
                DataManager.Instance.DefeatedEnemies.Add(
                    new ArchonsRise.SaveData.Cell(gridPos.x, gridPos.y));
            Destroy(this.gameObject);
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
        GameManager.Instance.CombatCanvasActive();
        yield return new WaitUntil(() => GameManager.Instance.combatCanvas.GetComponentInChildren<TextMeshProUGUI>().enabled == false);
        deck.GetNewEnemyCard(this);
    }
}
