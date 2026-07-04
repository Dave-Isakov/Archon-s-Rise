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
}
