using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EndTurnButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button endTurnButton;
    [SerializeField] VoidEvent endTheTurn;

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance.commands.ClearStack();
    }

    private void Start() 
    {
        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(() => endTheTurn.Raise());
    }   
}
