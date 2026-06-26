using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundImage : MonoBehaviour
{
    [SerializeField] List<Sprite> images;
    private Dictionary<EmpowerType, Sprite> backgroundDictionary;

    // private void Start()
    // {
    //     backgroundDictionary = new() { {EmpowerType.Green, images[0] }, {EmpowerType.Purple, images[1]}, {EmpowerType.Red, images[2]}, {EmpowerType.Yellow, images[3]}, {EmpowerType.None, images[4]} };
    // }

    // public void AdjustBackground(Card card)
    // {
    //     this.gameObject.GetComponent<Image>().sprite = backgroundDictionary[card.cardSO.empowerType];
    // }
}
