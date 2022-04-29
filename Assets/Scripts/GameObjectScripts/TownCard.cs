using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TownCard : MonoBehaviour
{
    public TownsSO townSO;
    [SerializeField] private TextMeshProUGUI townName;
    [SerializeField] private TextMeshProUGUI razeAmount;
    [SerializeField] private TextMeshProUGUI recruitLevel;
    [SerializeField] private TextMeshProUGUI description;


    void Start()
    {
        townName.text = townSO.cardName;
        razeAmount.text = "/#\\ " + townSO.razeLevel.ToString();
        recruitLevel.text = "(*) " + townSO.recruitLevel.ToString();
        description.text = townSO.cardDescription;
    }

    void Update()
    {
        
    }
}
