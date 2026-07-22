// Helpers for the [Flags] EmpowerType enum.
public static class EmpowerTypeExtensions
{
    // All four color flags set. "All" content (e.g. the Crystallization card and wild
    // crystals) stores this as -1: such a card is empowerable by any color, and such a
    // crystal empowers any card.
    const EmpowerType AllColors =
        EmpowerType.Red | EmpowerType.Yellow | EmpowerType.Green | EmpowerType.Purple;

    // True when the value carries every color flag (matches the serialized -1 "All").
    public static bool IsAllColors(this EmpowerType type) =>
        ((int)type & (int)AllColors) == (int)AllColors;

    // Tint hex for the shared Crystallize skill-token icon (house pattern: one purple
    // crystal glyph, recolored per crystalColor). Purple is the icon's native art color,
    // so it renders untinted, same as None/other values.
    public static string SkillIconTintHex(this EmpowerType color)
    {
        if (color == EmpowerType.Red)    return "FF0000";
        if (color == EmpowerType.Yellow) return "F5D90A";
        if (color == EmpowerType.Green)  return "46A758";
        return "";
    }
}
