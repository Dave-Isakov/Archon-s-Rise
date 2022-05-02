using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Crystal : MonoBehaviour, IPointerClickHandler
{
    CrystalInventory inventory;
    public EmpowerType color;
    public bool isAll;

    void Awake()
    {
        inventory = FindObjectOfType<CrystalInventory>();
    }
    public void RemoveCrystal()
    {
        inventory.crystalsInInventory.Remove(this);
        Destroy(this.gameObject);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        this.RemoveCrystal();
    }

    // public void EmpowerCrystal(Card card)
    // {
    //     if(this.color == card.cardSO.empowerType && this.color != EmpowerType.All)
    //     {
    //         playedCrystals.Push(this);
    //         Debug.Log(this.color.ToString());
    //         this.RemoveCrystal();
    //     }
    //     else if(this.color.HasFlag(EmpowerType.All))
    //     {
    //         playedCrystals.Push(this);
    //         Debug.Log(this.color.ToString());
    //         this.RemoveCrystal();
    //     }
    // }
    // public void RegenCrystal()
    // {
    //     var crystal = playedCrystals.Pop();
    //     if(!crystal.color.HasFlag(EmpowerType.All))
    //         inventory.CreateCrystal(crystal.color);
    //     else
    //         inventory.CreateCrystal(crystal.color);
    // }


}
