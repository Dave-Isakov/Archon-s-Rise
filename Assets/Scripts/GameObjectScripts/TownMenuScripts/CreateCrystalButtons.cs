using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CreateCrystalButtons : MonoBehaviour
{
    [SerializeField] EmpowerType color;
    [SerializeField] Button thisButton;
    [SerializeField] EmpowerColorEvent onCrystalButtonClick_CreateCrystalOfColor;

    private void Start() 
    {
        thisButton.onClick.RemoveAllListeners();
        thisButton.onClick.AddListener(() => onCrystalButtonClick_CreateCrystalOfColor.Raise(color));
    }
}
