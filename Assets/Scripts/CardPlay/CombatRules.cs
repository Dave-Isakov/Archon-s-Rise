// Pure combat resolution rules. No scene/Unity dependency so the wound-free
// Siege guarantee is unit-testable via the CLI pure-test harness.
public enum AttackKind { Normal, Siege }

public static class CombatRules
{
    // Normal spends the Attack pool; Siege spends the Siege pool. Either defeats
    // the enemy when its own pool covers the enemy's HP. Pools never cross over.
    public static bool CanDefeat(AttackKind kind, int attackPool, int siegePool, int enemyHP)
        => kind == AttackKind.Siege ? siegePool >= enemyHP : attackPool >= enemyHP;

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
}
