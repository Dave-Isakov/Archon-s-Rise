using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StatsDisplay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI attackText;
    [SerializeField] TextMeshProUGUI defendText;
    [SerializeField] TextMeshProUGUI influenceText;
    [SerializeField] TextMeshProUGUI exploreText;
    void Update()
    {
        attackText.text = "Attack: " + DataManager.Instance.playerAttack.ToString();
        defendText.text = "Defend: " + DataManager.Instance.playerDefend.ToString();
        influenceText.text = "Influence: " + DataManager.Instance.playerInfluence.ToString();
        exploreText.text = "Explore: " + DataManager.Instance.playerExplore.ToString();
    }
}
