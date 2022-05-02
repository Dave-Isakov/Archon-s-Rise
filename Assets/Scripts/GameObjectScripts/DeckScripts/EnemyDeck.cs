using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class EnemyDeck : Deck<EnemiesSO>, IPointerClickHandler
{
    public List<EnemiesSO> enemies = new List<EnemiesSO>();
    [SerializeField] GameObject prefabEnemyCard;
    [SerializeField] GameObject grid;
    [SerializeField] TextMeshProUGUI enemyText;
    private GameObject enemyCard;
    private int enemyID;
    

    void Start()
    {
        Shuffle(enemies);
        
    }

    void Update()
    {
        enemyText.text = enemies.Count.ToString();
    }

    public void GetNewEnemyCard()
    {
        if (enemies.Count >= 1)
            enemyCard = Instantiate(prefabEnemyCard, new Vector3(0,0,0), Quaternion.identity);
            enemyCard.name = enemyCard.name.ToString() + enemyID;
            enemyID++;
            enemyCard.transform.SetParent(grid.transform, false);
            enemyCard.GetComponent<EnemyCard>().enemySO = enemies[0];
            enemies.Remove(enemies[0]);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GetNewEnemyCard();
    }
}
