using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class ExplorationButton : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI exploreText;
    [SerializeField] Location locationDeck;
    [SerializeField] IntEvent OnClick_SendExploreCostForValidation;
    private int exploreCost;

    private void Start() {
        exploreCost = locationDeck.locations[0].exploreCost;
        GetComponent<Button>().onClick.AddListener(() => OnClick_SendExploreCostForValidation.Raise(exploreCost));
    }

    private void Update()
    {
        exploreText.text = "Explore! \n" + exploreCost.ToString();
    }
}
