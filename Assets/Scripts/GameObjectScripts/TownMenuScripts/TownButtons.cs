using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class TownButtons : MonoBehaviour
{
    [SerializeField] protected TownCard _town;
    [SerializeField] protected TownEvent townEvent;
    [SerializeField] protected Button thisButton;

    protected void Awake()
    {
        thisButton.onClick.AddListener(() => townEvent.Raise(_town));
    }
    public void SetTownCard(TownCard town)
    {
        this._town = town;
    }
}
