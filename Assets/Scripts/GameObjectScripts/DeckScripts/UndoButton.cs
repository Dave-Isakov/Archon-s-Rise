using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UndoButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button undoButton;

    public void OnPointerClick(PointerEventData eventData)
    {
        if(!undoButton.interactable)
            GameManager.Instance.ValidationMessage("There is nothing to undo.");
    }

    private void Start() 
    {
        undoButton.onClick.AddListener(() => GameManager.Instance.commands.UndoCommand());
    }

    private void Update()
    {
        if(GameManager.Instance.commands is not null)
        if (GameManager.Instance.commands.GetStackCount() <= 0)
        {
            undoButton.interactable = false;
        }
        else
        {
            undoButton.interactable = true;
        }
    }    
}
