using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Unit : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image image;
    [SerializeField] public UnitsSO unitSO;
    [SerializeField] TextMeshProUGUI unitLetter;
    [SerializeField] TextMeshProUGUI unitText;
    [SerializeField] UnitEvent onClick_PerformUnitAction;
    ICommands unitCommand;
    private bool isPlayed = false;
    public bool IsPlayed { get => isPlayed; set => isPlayed = value; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(!isPlayed)
        {
            unitCommand = new UnitCommand(onClick_PerformUnitAction, this);
            GameManager.Instance.commands.AddCommand(unitCommand);
        }
        else
            GameManager.Instance.ValidationMessage($"{unitSO.cardName} has already been played, undo to revert action.");
    }

    void Start()
    {
        image.color = unitSO.color;
        unitLetter.text = unitSO.unitLetter.ToString();
        unitText.text = unitSO.cardDescription;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        this.transform.localScale = new Vector3(2,2,2);
        GetComponent<Canvas>().overrideSorting = true;
        GetComponent<Canvas>().sortingOrder = 50;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        this.transform.localScale = new Vector3(1,1,1);
        GetComponent<Canvas>().overrideSorting = false;
        GetComponent<Canvas>().sortingOrder = 0;
    }

}
