using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TownCards", menuName = "ScriptableObjects/Cards/TownCards")]
public class TownsSO : AllCards
{
    public TownSize townSize;
    [Flags]
    public enum TownActivity
    {
        None = 0,
        Recruit = 1,
        Raze = 2,
        GatherIntel = 4,
        Heal = 8,
        Resources = 16
    }
    public TownActivity activity;

    public int recruitLevel;
    public int razeLevel;
    public int resourceLevel;
}
