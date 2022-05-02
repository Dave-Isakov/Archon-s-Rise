using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StatsDisplay : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] TextMeshProUGUI attackText;
    [SerializeField] TextMeshProUGUI defendText;
    [SerializeField] TextMeshProUGUI influenceText;
    [SerializeField] TextMeshProUGUI exploreText;
    void Update()
    {
        attackText.text = "Attack: " + player.PlayerAttack.ToString();
        defendText.text = "Defend: " + player.PlayerDefend.ToString();
        influenceText.text = "Influence: " + player.PlayerInfluence.ToString();
        exploreText.text = "Explore: " + player.PlayerExplore.ToString();
    }
}
