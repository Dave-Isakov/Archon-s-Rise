using UnityEngine;
using UnityEngine.UI;

// Shared card-display helpers so Card (interactive) and CardPreview (display-only)
// don't duplicate the empower-color logic.
public static class CardVisuals
{
    public static void ApplyEmpowerColor(GameObject card, EmpowerType type,
        Color green, Color red, Color purple, Color yellow)
    {
        var frontImage = card.GetComponentsInChildren<Image>()[0];
        switch (type)
        {
            case EmpowerType.Green:  frontImage.color = green;  break;
            case EmpowerType.Red:    frontImage.color = red;    break;
            case EmpowerType.Purple: frontImage.color = purple; break;
            case EmpowerType.Yellow: frontImage.color = yellow; break;
        }
    }
}
