using System;
using System.Collections.Generic;
using UnityEngine;

// Skill-pick screen (mirrors RewardCanvas: fixed slots, double-resolution guard).
public class LevelUpModal : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] SkillChoiceButton[] choiceSlots = new SkillChoiceButton[3];
    Action<SkillsSO> onChosen;
    bool resolved;

    public bool IsOpen => canvas != null && canvas.enabled;

    public void Offer(IReadOnlyList<SkillsSO> skills, Action<SkillsSO> onChosen)
    {
        this.onChosen = onChosen;
        resolved = false;
        canvas.enabled = true;

        for (int i = 0; i < choiceSlots.Length; i++)
        {
            bool active = i < skills.Count;
            choiceSlots[i].gameObject.SetActive(active);
            if (active) choiceSlots[i].Bind(skills[i], Choose);
        }
    }

    void Choose(SkillsSO chosen)
    {
        if (resolved) return;
        resolved = true;
        canvas.enabled = false;
        onChosen?.Invoke(chosen);
    }
}
