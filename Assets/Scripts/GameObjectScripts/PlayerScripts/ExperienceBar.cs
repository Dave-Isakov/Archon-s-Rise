using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ExperienceBar : MonoBehaviour
{
    [SerializeField] Player player;
    [SerializeField] TextMeshProUGUI experienceText;
    [SerializeField] Slider experienceBar;

    void Update()
    {
        experienceText.text = player.PlayerExp + "/" + player.ExpToNextLevel;
        experienceBar.maxValue = player.ExpToNextLevel;
        experienceBar.value = player.PlayerExp;
    }
}
