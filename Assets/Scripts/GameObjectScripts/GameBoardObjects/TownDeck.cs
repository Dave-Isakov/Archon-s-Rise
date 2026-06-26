using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TownDeck : Deck<TownsSO>
{
    public List<TownsSO> towns = new List<TownsSO>();
    [SerializeField] GameObject townCard;
    [SerializeField] GameObject townLayout;
    [SerializeField] TextMeshProUGUI townText;
    private GameObject town;
    void Start()
    {
        
    }


    void Update()
    {
        townText.text = towns.Count.ToString();
    }

    public TownCard CreateTown(TownToken townToken)
    {
        town = GameObject.Instantiate(townCard, GameManager.Instance.enlargeTownCardPosition.transform);
        town.name = town.name.ToString();
        var townCardComponent = town.GetComponent<TownCard>();
        townCardComponent.townSO = townToken.townSO;
        return townCardComponent;
    }

    public void SetTownToGrid(TownCard card)
    {
        card.gameObject.transform.SetParent(townLayout.transform, false);
        card.gameObject.transform.localScale = new Vector3(1, 1, 0);
    }
}
