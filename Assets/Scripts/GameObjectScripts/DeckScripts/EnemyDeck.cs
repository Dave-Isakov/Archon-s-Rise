using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public class EnemyDeck : Deck<EnemiesSO>, IPointerClickHandler
{
    public List<EnemiesSO> enemies = new List<EnemiesSO>();
    [SerializeField] GameObject prefabEnemyCard;
    // GuardianAssault spawns the same combat card without a scene reference.
    public GameObject PrefabEnemyCard => prefabEnemyCard;
    [SerializeField] GameObject prefabEnemyToken;
    [SerializeField] GameObject inPlayEnemies;
    [SerializeField] TextMeshProUGUI enemyText;
    private GameObject enemyCard;
    private GameObject enemyToken;
    private int enemyID;
    

    void Start()
    {
        // GetNewEnemyCard();
    }

    void Update()
    {
        enemyText.text = enemies.Count.ToString();
    }

    public void GetNewEnemyCard(EnemyToken token)
    {
        enemyCard = Instantiate(prefabEnemyCard, token.transform.position, Quaternion.identity, GameManager.Instance.enemyCardCombatPosition.transform);
        enemyCard.transform.localScale = new Vector3(1.75f,1.75f);
        enemyCard.name = enemyCard.name.ToString();
        var card = enemyCard.GetComponent<EnemyCard>();
        card.enemySO = token.enemy;
        card.bonusHP = token.bonusHP;         // set before the card's Start runs
        card.bonusAttack = token.bonusAttack;
        card.EnableCombat(token);
        token.cardRef = card;
    }

    public void GetNewEnemyToken(Vector3Int gridPosition, Tilemap ground, int enemyIndex,
        int bonusHP = 0, int bonusAttack = 0, bool isMidRunSpawn = false)
    {
        enemyToken = Instantiate(prefabEnemyToken, ground.CellToLocal(gridPosition), Quaternion.identity);
        enemyToken.name = enemyToken.name.ToString() + enemyID;
        enemyID++;
        enemyToken.transform.SetParent(inPlayEnemies.transform, false);
        var token = enemyToken.GetComponent<EnemyToken>();
        token.enemy = enemies[enemyIndex];
        token.bonusHP = bonusHP;
        token.bonusAttack = bonusAttack;
        token.isMidRunSpawn = isMidRunSpawn;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // GetNewEnemyCard();
    }
}