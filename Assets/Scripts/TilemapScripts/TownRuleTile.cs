using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Town Rule Tile", menuName = "ScriptableObjects/Tiles/Town Rule Tiles")]
public class TownRuleTile : HexRuleTile
{
    public TownsSO town;
}