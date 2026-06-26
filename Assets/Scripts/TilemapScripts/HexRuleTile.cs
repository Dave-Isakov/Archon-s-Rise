using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Hex Rule Tile", menuName = "ScriptableObjects/Tiles/Hex Rule Tiles")]
public class HexRuleTile : HexagonalRuleTile
{
    public int exploreCost;
    public TerrainType terrain;
}