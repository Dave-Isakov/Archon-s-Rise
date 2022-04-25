using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Player", menuName = "ScriptableObjects/PlayerSO")]
public class PlayerSO : ScriptableObject
{
    [SerializeField] string playerName;
    [SerializeField] int playerHandSize;
    [SerializeField] List<CardsSO> startingHand;

    public string PlayerName { get { return playerName;}}
    public int PlayerHandSize { get { return playerHandSize;}}

    public List<CardsSO> StartingHand {get {return startingHand;}}
    
    //[SerializeField] Character character;
}