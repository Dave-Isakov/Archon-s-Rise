using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Dungeon : MonoBehaviour, IPointerClickHandler
{
    public List<EnemiesSO> dungeonEnemies;
    public List<RewardsSO> rewards;
    private int enemyIndex = 0;
    public DungeonsSO dungeonSO;
    private int exploreCost;
    [SerializeField] GameObject prefabEnemyCard;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI numEnemiesText;
    [SerializeField] TextMeshProUGUI exploreText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] DungeonEvent onDungeonReward_RewardPlayer;

    public int EnemyIndex { get => enemyIndex; }
    public int ExploreCost { get => exploreCost; set => exploreCost = value; }

    private void Start()
    {
        nameText.text = dungeonSO.cardName;
        numEnemiesText.text = "Enemies: " + dungeonSO.enemies.Count.ToString();
        exploreText.text = "Explore: " + dungeonSO.exploreCost.ToString();
        descriptionText.text = dungeonSO.cardDescription;
        exploreCost = dungeonSO.exploreCost;

        foreach (var enemy in dungeonSO.enemies)
            dungeonEnemies.Add(enemy);

        foreach (var reward in dungeonSO.rewards)
            rewards.Add(reward);
    }

    public void NextDungeonEvent()
    {
        var rng = Random.Range(0,2);
        if (rng == 0 && rewards.Count !=0)
        {
            onDungeonReward_RewardPlayer.Raise(this);
        }
        else
            SpawnDungeonEnemy();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        NextDungeonEvent();
    }

    public void SpawnDungeonEnemy()
    {
        var enemyCard = Instantiate(prefabEnemyCard, new Vector3(0,0,0), Quaternion.identity);
        enemyCard.name = enemyCard.name.ToString() + enemyIndex;
        enemyCard.transform.SetParent(this.transform.parent, false);
        enemyCard.GetComponent<EnemyCard>().enemySO = dungeonEnemies[EnemyIndex];
        enemyIndex++;
    }

    public void RemoveReward(int index)
    {
        rewards.RemoveAt(index);
    }
}
