using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Crystal : MonoBehaviour, IPointerClickHandler
{
    CrystalInventory inventory;
    public EmpowerType color;
    public bool isAll;
    [SerializeField] private CanvasGroup canvasGroup; // optional; for dim visual

    // Display-only reservation. The crystal stays in inventory; this only signals
    // "this crystal will be spent when the empowered card is played." Real consume
    // is still CrystalInventory.EmpowerCrystal() at play time.
    public void SetReserved(bool reserved)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = reserved ? 0.4f : 1f;
    }

    void Awake()
    {
        inventory = FindAnyObjectByType<CrystalInventory>();
    }
    public void RemoveCrystal()
    {
        inventory.crystalsInInventory.Remove(this);
        this.gameObject.SetActive(false);
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
