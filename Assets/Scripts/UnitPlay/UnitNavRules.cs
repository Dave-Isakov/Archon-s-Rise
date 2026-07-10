// Pure gamepad focus for the unit pop-out: one vertical lane of option rows
// (0..optionCount-1) with the Use button as the final slot (== optionCount).
// dy follows InspectorNavRules' convention: +1 is up, -1 is down. Locked rows
// ARE focus targets (the player can read why they're locked); Use-ability is
// UnitPlaySelection's concern, not navigation's.
public static class UnitNavRules
{
    public static int UseSlot(int optionCount) => optionCount;

    public static int Open(int optionCount) => optionCount > 0 ? 0 : UseSlot(optionCount);

    public static int Move(int pos, int dy, int optionCount)
    {
        if (dy < 0) return pos < UseSlot(optionCount) ? pos + 1 : pos; // down
        if (dy > 0) return pos > 0 ? pos - 1 : pos;                   // up
        return pos;
    }
}
