using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TownCard : MonoBehaviour, IPointerClickHandler
{
    public TownsSO townSO;
    [SerializeField] private TextMeshProUGUI townName;
    [SerializeField] private TextMeshProUGUI razeAmount;
    [SerializeField] private TextMeshProUGUI recruitLevel;
    [SerializeField] private TextMeshProUGUI description;

    [Header("Events")]
    [SerializeField] TownEvent onClick_OpenTownMenu;
    [SerializeField] TownEvent onTownMenuOpen_MaxmizeTownCard;
    [SerializeField] TownEvent onClick_CloseTownMenu;
    [SerializeField] TownEvent onTownMenuClose_MinimizeTownCard;
    private bool isMaximized;

    public void OnPointerClick(PointerEventData eventData)
    {
        if(isMaximized)
        {
            onTownMenuClose_MinimizeTownCard.Raise(this);
            onClick_CloseTownMenu.Raise(this);
        }
        else if(!isMaximized)
        {
            onClick_OpenTownMenu.Raise(this);
            onTownMenuOpen_MaxmizeTownCard.Raise(this);
        }
    }

    void Start()
    {
        townName.text = townSO.cardName;
        razeAmount.text = "/#\\ " + townSO.razeLevel.ToString();
        recruitLevel.text = "(*) " + townSO.recruitLevel.ToString();
        description.text = townSO.cardDescription;
    }
    //Maximizes the card under the card menu canvas.
    public void SetCardObjectToMax(TownCard card)
    {
        card.gameObject.transform.SetParent(GameManager.Instance.enlargeTownCardPosition.transform, true);
        card.gameObject.transform.position = GameManager.Instance.enlargeTownCardPosition.transform.position;
        card.gameObject.transform.localScale = new Vector3(4, 4, 0);
        card.isMaximized = true;
    }

    //Returns the card to normal size in the player hand.
    public void SetCardObjectToNormal(TownCard card)
    {
        card.isMaximized = false;
    }
}
