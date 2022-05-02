using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TownDeck : Deck<TownsSO>, IPointerClickHandler
{
    public List<TownsSO> towns = new List<TownsSO>();
    [SerializeField] GameObject townCard;
    [SerializeField] GameObject townLayout;
    [SerializeField] TextMeshProUGUI townText;
    private GameObject town;
    private int townID;
    void Start()
    {
        
    }


    void Update()
    {
        townText.text = towns.Count.ToString();
    }

    public void CreateTown()
    {
        if(towns.Count >= 1)
        {
            town = Instantiate(townCard, new Vector3(0,0,0), Quaternion.identity);
            town.name = town.name.ToString() + townID;
            townID++;
            town.transform.SetParent(townLayout.transform, false);
            town.GetComponent<TownCard>().townSO = towns[0];
            towns.Remove(towns[0]);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CreateTown();
    }

    public void SetTownToGrid(TownCard card)
    {
        card.gameObject.transform.SetParent(townLayout.transform, false);
        card.gameObject.transform.localScale = new Vector3(1, 1, 0);
    }
}
