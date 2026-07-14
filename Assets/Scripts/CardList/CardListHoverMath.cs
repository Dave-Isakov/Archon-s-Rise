// Offset pulling a hovered card-list card toward the viewport centre,
// proportional to its distance: edge cards lean well in, a centred card only
// scales in place. Pure, no scene dependency.
public static class CardListHoverMath
{
    public static void PullOffset(float cardX, float cardY, float centerX, float centerY,
        float strength, out float offsetX, out float offsetY)
    {
        offsetX = (centerX - cardX) * strength;
        offsetY = (centerY - cardY) * strength;
    }
}
