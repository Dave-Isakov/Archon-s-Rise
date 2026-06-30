using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class Crystal : MonoBehaviour, IPointerClickHandler
{
    CrystalInventory inventory;
    public EmpowerType color;
    public bool isAll;
    [SerializeField] private CanvasGroup canvasGroup; // optional; for dim visual

    Vector3 _homePos;
    Vector3 _homeScale;

    // Display-only reservation. The crystal stays in inventory; this only signals
    // "this crystal will be spent when the empowered card is played." Real consume
    // is still CrystalInventory.EmpowerCrystal() at play time.
    public void SetReserved(bool reserved)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = reserved ? 0.4f : 1f;
    }

    // Play flourish: drain toward the played card, then hide. Restores the original
    // local pose on complete so a later RegenCrystal shows the crystal back in its slot.
    public void FlySpendThenHide(Vector3 worldTarget)
    {
        var t = transform;
        _homePos = t.localPosition;
        _homeScale = t.localScale;
        t.DOKill();
        t.DOMove(worldTarget, 0.3f).SetEase(Ease.InBack);
        t.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)
         .OnComplete(() =>
         {
             t.localPosition = _homePos;
             t.localScale = _homeScale;
             gameObject.SetActive(false);
         });
    }

    // Undo flourish: pop the regenerated crystal back in at its slot.
    public void PopIn()
    {
        var t = transform;
        t.DOKill();
        t.localPosition = _homePos;  // restore home slot position (safe even if FlySpendThenHide was never called: Vector3.zero is fine)
        Vector3 homeScale = _homeScale == Vector3.zero ? Vector3.one : _homeScale;
        t.localScale = Vector3.zero;
        t.DOScale(homeScale, 0.25f).SetEase(Ease.OutBack);
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
