using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RewardCanvas : MonoBehaviour
{
    [SerializeField] GameObject[] cardLocations = new GameObject[3];
    [SerializeField] GameObject cardPrefab;

    public void SetCardRewards()
    {
        foreach (var i in cardLocations)
        {
            var playerCard = Instantiate(cardPrefab, new Vector3(0,0,0), Quaternion.identity);
            playerCard.transform.SetParent(i.transform, false);
            playerCard.transform.localScale = new Vector3(3,3,3);
            playerCard.GetComponent<Card>().cardSO = DataManager.Instance.allCards[Random.Range(0, DataManager.Instance.allCards.Length)];
            playerCard.GetComponent<Card>().IsReward = true;
        }
    }
}
