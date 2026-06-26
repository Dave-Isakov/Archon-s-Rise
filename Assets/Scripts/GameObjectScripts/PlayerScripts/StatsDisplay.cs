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
        attackText.text = player.PlayerAttack.ToString();
        defendText.text = player.PlayerDefend.ToString();
        influenceText.text = player.PlayerInfluence.ToString();
        exploreText.text = player.PlayerExplore.ToString();
    }
}
