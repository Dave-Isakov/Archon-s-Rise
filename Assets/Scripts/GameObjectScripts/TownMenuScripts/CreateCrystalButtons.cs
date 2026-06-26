using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CreateCrystalButtons : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] EmpowerType color;
    [SerializeField] Button thisButton;
    [SerializeField] EmpowerColorEvent onCrystalButtonClick_CreateCrystalOfColor;

    public void OnPointerClick(PointerEventData eventData)
    {
        foreach(var crystal in FindObjectsOfType<CreateCrystalButtons>())
        {
            Destroy(crystal.gameObject);
        }
    }

    private void Start() 
    {
        thisButton.onClick.RemoveAllListeners();
        thisButton.onClick.AddListener(() => onCrystalButtonClick_CreateCrystalOfColor.Raise(color));
    }

    private void Update() {
        if(!GameManager.Instance.townCanvas.enabled)
        {
            thisButton.interactable = false;
        }
    }
}
