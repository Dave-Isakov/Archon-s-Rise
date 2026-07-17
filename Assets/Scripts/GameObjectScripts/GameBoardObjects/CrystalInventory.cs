using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CrystalInventory : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] List<GameObject> crystals = new();
    [SerializeField] VoidEvent onCrystalGainedTutorial; // M2.12 one-shot trigger
    public List<Crystal> crystalsInInventory;
    private int crystalID;
    GameObject activeCrystal;
    public Card _card;
    public Stack<Crystal> playedCrystals = new();
    public Stack<Crystal> playerCreatedCrystal = new();
    // Crystals granted by skill activations, so a skill undo removes exactly
    // the crystals it created (mirrors playerCreatedCrystal for Crystallize
    // cards; command-stack LIFO order keeps push/pop pairs matched).
    public Stack<Crystal> skillCreatedCrystals = new();

    // private void Start() 
    // {
    //     foreach(EmpowerType i in Enum.GetValues(typeof(EmpowerType)))
    //     {
    //         CreateCrystal(i);
    //     }
    // }
    public Crystal CreateCrystal(EmpowerType color)
    {
        switch (color)
        {
            case EmpowerType.Green:
                activeCrystal = Instantiate(crystals[0], this.gameObject.transform);
                break;
            case EmpowerType.Purple:
                activeCrystal = Instantiate(crystals[1], this.gameObject.transform);
                break;
            case EmpowerType.Red:
                activeCrystal = Instantiate(crystals[2], this.gameObject.transform);
                break;
            case EmpowerType.Yellow:
                activeCrystal = Instantiate(crystals[3], this.gameObject.transform);
                break;
            default:
                activeCrystal = Instantiate(crystals[4], this.gameObject.transform);
                break;
        }
        activeCrystal.name += crystalID;
        crystalID++;
        crystalsInInventory.Add(activeCrystal.GetComponent<Crystal>());
        // Save restore also funnels through here (SetCounts) — a load must
        // never look like a fresh gain.
        if (onCrystalGainedTutorial != null
            && (DataManager.Instance == null || !DataManager.Instance.IsLoading))
            onCrystalGainedTutorial.Raise();
        return activeCrystal.GetComponent<Crystal>();
    }

    public void ToggleEmpowered(Toggle empower)
    {
        if (!_card.IsEmpowered && crystalsInInventory.Exists(c => c.color == _card.cardSO.empowerType || c.isAll))
        {
            empower.isOn = true;
            _card.IsEmpowered = true;
        }
        else
        {
            GameManager.Instance.ValidationMessage($"You cannot empower without {_card.cardSO.empowerType} crystals or an Allcrystal!");
            empower.isOn = false;
        }
    }

    public void SetCard(Card card)
    {
        _card = card;
    }

    public void EmpowerCrystal()
    {
        var crystal = SelectEmpowerCrystal();
        if (crystal is null) return; // No matching or wild crystal to spend; nothing to consume.

        playedCrystals.Push(crystal);
        Debug.Log(crystal.color.ToString());

        // Same removal as RemoveCrystal() (list remove + deactivate), but the deactivate
        // is deferred to the end of the drain flourish toward the played card.
        crystalsInInventory.Remove(crystal);
        Vector3 target = _card != null ? _card.transform.position : crystal.transform.position;
        crystal.FlySpendThenHide(target);
    }

    public void RegenCrystal()
    {
        var crystal = playedCrystals.Pop();
        crystal.gameObject.SetActive(true);
        crystalsInInventory.Add(crystal);
        crystal.PopIn();
        Debug.Log(crystal.color);
    }

    public Crystal SelectEmpowerCrystal() => SelectPayCrystal(_card.cardSO.empowerType);

    // Generalized "find a crystal that satisfies this cost" — same preference
    // order as card empower: exact color first, wild as the fallback.
    public Crystal SelectPayCrystal(EmpowerType cost)
    {
        foreach (var crystal in crystalsInInventory)
            if (!crystal.isAll && ColorSatisfies(crystal.color, cost))
                return crystal;
        foreach (var crystal in crystalsInInventory)
            if (crystal.isAll)
                return crystal;
        return null;
    }

    public bool CanPay(EmpowerType cost)
        => cost == EmpowerType.None || SelectPayCrystal(cost) != null;

    // Unit option costs/grants get their own LIFO stacks (mirroring
    // playedCrystals / skillCreatedCrystals) so undo pops exactly what the
    // command pushed.
    public Stack<Crystal> unitSpentCrystals = new();
    public Stack<Crystal> unitCreatedCrystals = new();

    public void SpendUnitCrystal(Crystal crystal, Vector3 flyTarget)
    {
        unitSpentCrystals.Push(crystal);
        crystalsInInventory.Remove(crystal);
        crystal.SetReserved(false);
        crystal.FlySpendThenHide(flyTarget);
    }

    public void RefundUnitCrystal()
    {
        if (unitSpentCrystals.Count == 0) return;
        var crystal = unitSpentCrystals.Pop();
        crystal.gameObject.SetActive(true);
        crystalsInInventory.Add(crystal);
        crystal.PopIn();
    }

    public void UnitCrystallize(EmpowerType color)
    {
        unitCreatedCrystals.Push(CreateCrystal(color));
    }

    public void UndoUnitCrystallize()
    {
        if (unitCreatedCrystals.Count == 0) return;
        unitCreatedCrystals.Pop().RemoveCrystal();
    }

    // A crystal satisfies a card when the card accepts any color (empowerType has
    // all color flags set, i.e. the -1/All case) or the colors match exactly.
    static bool ColorSatisfies(EmpowerType crystalColor, EmpowerType cardType)
    {
        if(cardType.IsAllColors()) return true;
        return crystalColor == cardType;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Intentionally empty: crystals are gained via cards/towns/rewards, not by clicking the inventory.
    }

    /// <summary>
    /// Returns one count per EmpowerType enum value (in enum declaration order),
    /// plus a trailing slot for wild ("All") crystals.
    /// EmpowerType is a [Flags] enum with non-contiguous values (None=0,Red=1,Yellow=2,Green=4,Purple=8),
    /// so we use Array.IndexOf rather than a raw (int) cast to map color to index.
    /// Wild crystals carry color == -1 (all flags), which is not an enum value and so
    /// has no color bucket; they are counted separately by isAll into the trailing slot.
    /// </summary>
    public int[] GetCounts()
    {
        var values = Enum.GetValues(typeof(EmpowerType));
        var counts = new int[values.Length + 1]; // +1: trailing wild-crystal slot
        foreach (var crystal in crystalsInInventory)
        {
            if (crystal == null) continue;
            if (crystal.isAll) { counts[values.Length]++; continue; }
            int idx = Array.IndexOf(values, crystal.color);
            if (idx >= 0) counts[idx]++;
        }
        return counts;
    }

    public void SetCounts(int[] counts)
    {
        // Clear current inventory GameObjects, then recreate per color.
        foreach (var c in new List<Crystal>(crystalsInInventory))
            if (c != null) Destroy(c.gameObject);
        crystalsInInventory.Clear();

        var values = Enum.GetValues(typeof(EmpowerType));
        for (int i = 0; i < counts.Length && i < values.Length; i++)
            for (int n = 0; n < counts[i]; n++)
                CreateCrystal((EmpowerType)values.GetValue(i));

        // Trailing wild slot: present in saves written after this fix; older saves
        // have no such slot and are simply skipped. -1 (all flags) hits CreateCrystal's
        // default case, which instantiates the WildCrystal prefab (color -1, isAll).
        if (counts.Length > values.Length)
            for (int n = 0; n < counts[values.Length]; n++)
                CreateCrystal((EmpowerType)(-1));
    }

    public void Crystallize(Card card)
    {
        if(card.cardSO.cardType.HasFlag(StatType.Crystal) && card.IsPlayed)
        {
            if(!card.IsEmpowered)
                for(var i = 0; i < card.cardSO.numCrystals; i++)
                {
                    var crystal = CreateCrystal(card.cardSO.empowerType);
                    playerCreatedCrystal.Push(crystal);
                }
            else if(card.IsEmpowered)
                for(var i = 0; i < card.cardSO.empowerNumCrystals; i++)
                {
                    var crystal = CreateCrystal(card.cardSO.empowerType);
                    playerCreatedCrystal.Push(crystal);
                }
        }

        else if(card.cardSO.cardType.HasFlag(StatType.Crystal) && !card.IsPlayed)
        {
            if(!card.IsEmpowered)
                for(var i = 0; i < card.cardSO.numCrystals; i++)
                {
                    var crystal = playerCreatedCrystal.Pop();
                    crystal.RemoveCrystal();
                }
            else if(card.IsEmpowered)
                for(var i = 0; i < card.cardSO.empowerNumCrystals; i++)
                {
                    var crystal = playerCreatedCrystal.Pop();
                    crystal.RemoveCrystal();
                }
        }
    }

    public void PurchaseTownCrystal(EmpowerType type)
    {
        CreateCrystal(type);
    }

    public void SkillCrystallize(EmpowerType color)
    {
        skillCreatedCrystals.Push(CreateCrystal(color));
    }

    public void UndoSkillCrystallize()
    {
        if (skillCreatedCrystals.Count == 0) return;
        skillCreatedCrystals.Pop().RemoveCrystal();
    }

    public void CleanUp()
    {
        foreach(var inactiveCrystal in FindObjectsByType<Crystal>(FindObjectsInactive.Include))
        {
            if(!inactiveCrystal.gameObject.activeSelf)
                Destroy(inactiveCrystal.gameObject);
        }
    }
}
