// When a HUD stat icon is allowed to play its increase animation. Pure, no scene
// dependency — the single gate behind every PlayerIcon pulse.
//
// Two traps this closes. First, StatType.None: HasFlag(None) is always true, so a
// duplicated PlayerIcon whose type was never reassigned pulses on every play.
// Second, undo: PlayCommand.Undo() re-raises the SAME event as Execute(), and the
// stat listeners tell the two apart via Card.IsPlayed while the animation ones
// never did — so undo replayed the increase while the number counted down.
// Pulses are apply-only, matching the "+N" echoes and Player.PulseStatIcon.
public static class IconPulseRules
{
    public static bool ShouldPulse(StatType source, StatType iconType, bool isUndo)
    {
        if (isUndo) return false;
        if (iconType == StatType.None || source == StatType.None) return false;
        return (source & iconType) == iconType;
    }
}
