// Pure combat resolution rules. No scene/Unity dependency so the wound-free
// Siege guarantee is unit-testable via the CLI pure-test harness.
public enum AttackKind { Normal, Siege }

public static class CombatRules
{
    // Siege is the stronger currency: a Normal attack may spend Siege to cover an
    // Attack shortfall, so it defeats when Attack + Siege together cover the HP.
    // Siege attacks still spend only the Siege pool; Attack never pays for Siege.
    public static bool CanDefeat(AttackKind kind, int attackPool, int siegePool, int enemyHP)
        => kind == AttackKind.Siege ? siegePool >= enemyHP : attackPool + siegePool >= enemyHP;

    // How much Siege a Normal attack borrows: Attack drains first, Siege covers
    // only the remaining shortfall so the more valuable pool is preserved.
    public static int SiegeSpentOnNormal(int attackPool, int enemyHP)
        => enemyHP > attackPool ? enemyHP - attackPool : 0;

    // The counterattack wound the player takes on a defeat. Siege is always
    // wound-free. Normal wounds when Defend falls short of the enemy's Attack,
    // one wound per HP-sized bite of the shortfall (matches the original loop).
    public static int WoundCount(AttackKind kind, int defend, int enemyAttack, int playerHP)
    {
        if (kind == AttackKind.Siege) return 0;
        if (defend >= enemyAttack) return 0;
        int wounds = 0;
        for (int i = 0; i < enemyAttack - defend; i += playerHP) wounds++;
        return wounds;
    }

    // The group counterattack: every surviving enemy hits at once, so their
    // Attack sums into ONE comparison against Defend, then the existing HP-bite
    // rule applies. Because Siege/Influence remove enemies before Engage, a
    // thinner survivor set means a smaller total and fewer wounds.
    public static int GroupWoundCount(int defend, int totalEnemyAttack, int playerHP)
        => WoundCount(AttackKind.Normal, defend, totalEnemyAttack, playerHP);
}
