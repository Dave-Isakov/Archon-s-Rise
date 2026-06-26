using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DungeonDeck : Deck<DungeonsSO>
{
    public List<DungeonsSO> dungeons = new();
    [SerializeField] GameObject prefabDungeon;
    // [SerializeField] TextMeshProUGUI enemyText;
    private GameObject dungeonCard;
    private int dungeonID;

    void Start()
    {
        Shuffle(dungeons);
        // GetNewDungeon();
    }

    void Update()
    {
        // enemyText.text = enemies.Count.ToString();
    }

    public void GetNewDungeon()
    {
        if (dungeons.Count >= 1)
            dungeonCard = Instantiate(prefabDungeon, new Vector3(0,0,0), Quaternion.identity);
            dungeonCard.name = dungeonCard.name.ToString() + dungeonID;
            dungeonID++;
            dungeonCard.transform.SetParent(this.transform, false);
            dungeonCard.GetComponent<Dungeon>().dungeonSO = dungeons[0];
            dungeons.Remove(dungeons[0]);
    }
}
