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
    [SerializeField] private TextMeshProUGUI description;

    public void OnPointerClick(PointerEventData eventData)
    {
        Destroy(this.gameObject);
        GameManager.Instance.townCanvas.enabled = false;
    }

    void Start()
    {
        townName.text = townSO.cardName;
        razeAmount.text = "/#\\ " + townSO.cardLevel.ToString();
        description.text = townSO.cardDescription;
    }

    public void SetCardObjectToMax(TownCard card)
    {
        card.gameObject.transform.SetParent(GameManager.Instance.enlargeTownCardPosition.transform, true);
        // Centre on the slot via LOCAL origin, not the slot's world position: the latter
        // reads a stale (0,0) on the first open before the canvas has laid out, leaving the
        // card off-centre. Local (0,0,0) is always the slot centre. (Mirrors Card.SetCardObjectToMax.)
        card.gameObject.transform.localPosition = Vector3.zero;
        card.gameObject.transform.localScale = new Vector3(4, 4, 0);
    }
}
