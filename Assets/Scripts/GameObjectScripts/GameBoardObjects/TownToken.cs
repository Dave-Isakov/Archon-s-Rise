using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TownToken : MonoBehaviour, IPointerClickHandler
{
    public TownsSO townSO;
    [SerializeField] TownDeck deck;
    [SerializeField] TownEvent onClick_OpenTownMenu;
    [SerializeField] TownEvent onClick_GetTownData;

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance.townCanvas.enabled = true;
        deck.CreateTown(this);
        onClick_GetTownData.Raise(this);
        onClick_OpenTownMenu.Raise(this);
    }
}
