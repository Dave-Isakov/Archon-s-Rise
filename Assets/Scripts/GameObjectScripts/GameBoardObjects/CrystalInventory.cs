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
    public List<Crystal> crystalsInInventory;
    private int crystalID;
    GameObject activeCrystal;
    public Card _card;
    public Stack<Crystal> playedCrystals = new();
    public Stack<Crystal> playerCreatedCrystal = new();

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
                activeCrystal = Instantiate(crystals[0], new Vector3(0, 0 ,0), Quaternion.identity);
                break;
            case EmpowerType.Purple:
                activeCrystal = Instantiate(crystals[1], new Vector3(0, 0 ,0), Quaternion.identity);
                break;
            case EmpowerType.Red:
                activeCrystal = Instantiate(crystals[2], new Vector3(0, 0 ,0), Quaternion.identity);
                break;
            case EmpowerType.Yellow:
                activeCrystal = Instantiate(crystals[3], new Vector3(0, 0 ,0), Quaternion.identity);
                break;
            default:
                activeCrystal = Instantiate(crystals[4], new Vector3(0, 0 ,0), Quaternion.identity);
                break;
        }
        activeCrystal.transform.SetParent(this.gameObject.transform);
        activeCrystal.name += crystalID;
        crystalID++;
        crystalsInInventory.Add(activeCrystal.GetComponent<Crystal>());
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
        if(crystal is null)
        {
            crystal = FindObjectOfType<AllCrystal>();
        }
        playedCrystals.Push(crystal);
        Debug.Log(crystal.color.ToString());
        crystal.RemoveCrystal();
        // foreach(var crystal in crystalsInInventory)
        // {
        //     if(!crystal.isAll)
        //         if(crystal.color == _card.cardSO.empowerType)    
        //         {
        //             playedCrystals.Push(crystal);
        //             Debug.Log(crystal.color.ToString());
        //             crystal.RemoveCrystal();
        //             break;
        //         }
        //     else if(crystal.isAll)
        //     {
        //         playedCrystals.Push(crystal);
        //         Debug.Log(crystal.color.ToString());
        //         crystal.RemoveCrystal();
        //         break;
        //     }
        // }
    }

    public void RegenCrystal()
    {
        var crystal = playedCrystals.Pop();
        crystal.gameObject.SetActive(true);
        crystalsInInventory.Add(crystal);
        // if(crystal.isAll)
        //     CreateCrystal(EmpowerType.None);
        // else
        //     CreateCrystal(crystal.color);
        Debug.Log(crystal.color);
    }

    public Crystal SelectEmpowerCrystal()
    {
        foreach(var crystal in crystalsInInventory)
        {
            if(crystal.color == _card.cardSO.empowerType)    
                return crystal;
        }
        return null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        EmpowerType[] i = new[] { EmpowerType.Green, EmpowerType.Yellow, EmpowerType.Red, EmpowerType.Purple, EmpowerType.None };
        CreateCrystal(i[UnityEngine.Random.Range(0,5)]);
    }

    public void Crystallize(Card card)
    {
        if(card.cardSO.cardType == StatType.Crystal && card.IsPlayed)
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

        else if(card.cardSO.cardType == StatType.Crystal && !card.IsPlayed)
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

    public void CleanUp()
    {
        foreach(var inactiveCrystal in FindObjectsOfType<Crystal>(true))
        {
            if(!inactiveCrystal.gameObject.activeSelf)
                Destroy(inactiveCrystal.gameObject);
        }
    }
}
