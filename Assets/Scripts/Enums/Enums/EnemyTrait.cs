// Authored enemy traits (spec 2026-07-29). APPEND-ONLY: new members go at the
// end. Self traits make one enemy harder to remove; aura traits grant a self
// trait to the whole roster, which is why granting is a bitwise OR and can
// never double-stack.
[System.Flags]
public enum EnemyTrait
{
    None = 0,
    // self
    Armored  = 1,    Elusive = 2,     Hulking  = 4,     Swift    = 8,
    Brutal   = 16,   Toxic   = 32,    Leech    = 64,    Harrying = 128,
    Vengeful = 256,
    // aura
    Warlord  = 512,  Miasma  = 1024,  Ironclad = 2048,  Outrider = 4096,
}
