using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillChoiceButton : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI descriptionText;

    public void Bind(SkillsSO skill, Action<SkillsSO> onClick)
    {
        if (icon != null && skill.icon != null) icon.sprite = skill.icon;
        if (icon != null)
        {
            string hex = skill.crystalColor.SkillIconTintHex();
            icon.color = hex.Length > 0 && ColorUtility.TryParseHtmlString("#" + hex, out Color tint)
                ? tint
                : Color.white;
        }
        nameText.text = skill.cardName;
        descriptionText.text = skill.cardDescription;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(skill));
    }
}
