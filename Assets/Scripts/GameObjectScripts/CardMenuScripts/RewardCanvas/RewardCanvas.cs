using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RewardCanvas : MonoBehaviour
{
    [SerializeField] GameObject[] cardLocations = new GameObject[3];
    [SerializeField] GameObject cardPrefab;
    private List<GameObject> cardRewards = new();

    public void SetCardRewards()
    {
        foreach (var i in cardLocations)
        {
            var playerCard = Instantiate(cardPrefab, new Vector3(0,0,0), Quaternion.identity);
            playerCard.transform.SetParent(i.transform, false);
            playerCard.transform.localScale = new Vector3(3,3,3);
            var cards = DataManager.Instance.Cards.Items;
            playerCard.GetComponent<Card>().cardSO = cards[Random.Range(0, cards.Count)];
            playerCard.GetComponent<Card>().IsReward = true;
        }
    }

    public void RemovePreviousRewards()
    {
        foreach (var i in cardLocations)
        {
            if(i.GetComponentInChildren<Card>() is not null)
                Destroy(i.GetComponentInChildren<Card>().gameObject);
        }
    }
}
