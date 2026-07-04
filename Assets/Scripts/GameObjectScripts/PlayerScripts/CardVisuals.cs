using UnityEngine;
using UnityEngine.UI;

// Shared card-display helpers so Card (interactive) and CardPreview (display-only)
// don't duplicate the empower-color logic.
public static class CardVisuals
{
    // Seconds per color for the "All" card shift; full pass = colors.Length * this.
    const float ShiftSecondsPerColor = 1.5f;

    public static void ApplyEmpowerColor(GameObject card, EmpowerType type,
        Color green, Color red, Color purple, Color yellow)
    {
        var frontImage = card.GetComponentsInChildren<Image>()[0];

        // An "All" card (empowerType carries every color flag, stored as -1) is every
        // color, so it cycles through them instead of showing one static color.
        if (type.IsAllColors())
        {
            var shift = frontImage.GetComponent<EmpowerColorShift>();
            if (shift == null) shift = frontImage.gameObject.AddComponent<EmpowerColorShift>();
            shift.Play(frontImage, new[] { red, yellow, green, purple }, ShiftSecondsPerColor);
            return;
        }

        // Fixed-color card: stop any cycle a previous binding may have started, then tint.
        var running = frontImage.GetComponent<EmpowerColorShift>();
        if (running != null) running.Stop();

        switch (type)
        {
            case EmpowerType.Green:  frontImage.color = green;  break;
            case EmpowerType.Red:    frontImage.color = red;    break;
            case EmpowerType.Purple: frontImage.color = purple; break;
            case EmpowerType.Yellow: frontImage.color = yellow; break;
        }
    }

    public static void ApplyWoundStyle(GameObject card, Color woundGrey)
    {
        var frontImage = card.GetComponentsInChildren<Image>()[0];
        frontImage.color = woundGrey;
    }
}
