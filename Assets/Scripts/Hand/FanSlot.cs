using UnityEngine;

// One card's computed place in the fan. Pure data — no scene dependency.
public readonly struct FanSlot
{
    public readonly Vector2 AnchoredPosition; // local, relative to hand-container center
    public readonly float TiltZ;              // degrees, +counter-clockwise

    public FanSlot(Vector2 anchoredPosition, float tiltZ)
    {
        AnchoredPosition = anchoredPosition;
        TiltZ = tiltZ;
    }
}

// Tunable fan geometry. Plain fields so HandFanLayout can serialize it.
[System.Serializable]
public class FanSettings
{
    public float SpreadDegrees = 66f; // total fan angle -> edges sit at ±33°
    public float CardSpacing = 120f;  // horizontal px between adjacent card centers
    public float ArcDrop = 40f;       // px the edge cards sit below the center card
}
