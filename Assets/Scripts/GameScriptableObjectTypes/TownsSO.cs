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
        Cards = 2,
        // Raze = 2,
        // GatherIntel = 4,
        Heal = 4,
        Resources = 8
    }
    public TownActivity activity;
    // Typed place taxonomy (M2). Service availability derives from this via
    // PlaceRules, superseding the legacy activity flags below (kept because
    // CrystalButton still reads Resources from them).
    public PlaceType placeType;
    // Conquest roster, fought in order; empty for a Town. Guardian counts are
    // data-driven: assault logic reads guardians.Count, never a constant.
    public List<EnemiesSO> guardians = new List<EnemiesSO>();
    public List<UnitsSO> recruitableUnits;
    public int recruitLevel;
    public int cardLevel;
    public int resourceLevel;
    public int healLevel;
}
