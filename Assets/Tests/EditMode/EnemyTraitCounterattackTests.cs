using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitCounterattackTests
{
    static EnemyTraitTuning T() => new EnemyTraitTuning();
    static EnemyCombatant E(int atk, EnemyTrait traits = EnemyTrait.None, bool blocked = false) =>
        new EnemyCombatant { Attack = atk, HP = atk, Traits = traits, Blocked = blocked };

    static int Wounds(List<EnemyCombatant> r, int defend, int toughness) =>
        EnemyTraitRules.HandWounds(EnemyTraitRules.BuildPreview(r, T()), defend, toughness);

    // --- spec 4.5 rows 1-2: Swift punishes the near-miss, never the whiff ---
    [Test]
    public void Row1_Swift_Defend5_OneWound()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        Assert.AreEqual(1, Wounds(r, 5, 2));
    }

    [Test]
    public void Row2_Swift_Defend0_NeverWorseThanPlain()
    {
        var swift = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        var plain = new List<EnemyCombatant> { E(3) };
        Assert.AreEqual(2, Wounds(swift, 0, 2));
        Assert.AreEqual(Wounds(plain, 0, 2), Wounds(swift, 0, 2));
    }

    // --- spec 4.5 rows 3-5: Brutal's surcharge bypasses the cap ---
    [Test]
    public void Row3_Brutal_FullyBlocked_ZeroWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(0, Wounds(r, 4, 2));
    }

    [Test]
    public void Row4_Brutal_Defend2_ThreeWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(3, Wounds(r, 2, 2)); // (8-2)/2
    }

    [Test]
    public void Row5_Brutal_Defend0_FourWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        Assert.AreEqual(4, Wounds(r, 0, 2)); // (8-0)/2
    }

    [Test]
    public void Row9_Warlord_AuraIsRealAttack()
    {
        var r = new List<EnemyCombatant> { E(2, EnemyTrait.Warlord), E(4) };
        Assert.AreEqual(4, Wounds(r, 0, 2)); // threat 2+5=7 -> bite(7,2)=4
    }

    [Test]
    public void Row10_SwiftPlusBrutal_WallAndCliffCompose()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Swift | EnemyTrait.Brutal) };
        Assert.AreEqual(4, Wounds(r, 5, 2)); // threat 8, short 3, cap 4->3, +4 = 7 -> 4
    }

    // --- the cap and surcharge in isolation ---
    [Test]
    public void Cap_NeverExceedsTotalBasis()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(3, EnemyTraitRules.Effective(p, 0), "capped at basis, not threat");
    }

    [Test]
    public void FullyBlockedGroup_ShortCircuits_NoSurcharge()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(0, EnemyTraitRules.Effective(p, 99));
    }

    // --- blocking (spec 7) ---
    [Test]
    public void BlockedEnemy_ContributesNothing()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal, blocked: true), E(3) };
        var p = EnemyTraitRules.BuildPreview(r, T());
        Assert.AreEqual(3, p.UnblockedThreat);
        Assert.AreEqual(3, p.UnblockedBasis);
        Assert.AreEqual(0, p.BrutalSurcharge, "a blocked Brutal must not surcharge");
    }

    [Test]
    public void BlockingEveryone_YieldsZeroWounds()
    {
        var r = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal, true), E(3, EnemyTrait.Toxic, true) };
        Assert.AreEqual(0, Wounds(r, 0, 2));
    }

    // --- spec 7.3: traits decide what is worth blocking ---
    [Test]
    public void BlockingBrutal_PreventsMoreThanItCosts()
    {
        var unblocked = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal) };
        var blocked   = new List<EnemyCombatant> { E(4, EnemyTrait.Brutal, blocked: true) };
        int cost = EnemyTraitRules.Threat(0, unblocked, T());       // 4
        Assert.AreEqual(4, cost);
        // Spending 4 Defend to block removes 8 of punishment.
        Assert.AreEqual(4, Wounds(unblocked, 0, 2));
        Assert.AreEqual(0, Wounds(blocked, 0, 2));
        // Soaking with the same 4 Defend instead leaves more damage standing.
        Assert.Greater(Wounds(unblocked, cost, 2), Wounds(blocked, 0, 2));
    }

    [Test]
    public void BlockingSwift_IsATrap_SoakingIsBetter()
    {
        var r = new List<EnemyCombatant> { E(3, EnemyTrait.Swift) };
        int blockCost = EnemyTraitRules.Threat(0, r, T());          // 6
        Assert.AreEqual(6, blockCost);
        // Spending the 6 it costs to block, but soaking with it instead,
        // already yields zero wounds — so the block bought nothing.
        Assert.AreEqual(0, Wounds(r, blockCost, 2));
    }

    // --- THE compatibility guarantee: no traits, no blocks == today's math ---
    [Test]
    public void NoTraitsNoBlocks_MatchesLegacyGroupWoundCount()
    {
        for (int atkA = 0; atkA <= 6; atkA++)
        for (int atkB = 0; atkB <= 6; atkB++)
        for (int defend = 0; defend <= 12; defend++)
        for (int tough = 1; tough <= 3; tough++)
        {
            var r = new List<EnemyCombatant> { E(atkA), E(atkB) };
            int legacy = CombatRules.GroupWoundCount(defend, atkA + atkB, tough);
            Assert.AreEqual(legacy, Wounds(r, defend, tough),
                $"drift at atk {atkA}+{atkB}, defend {defend}, toughness {tough}");
        }
    }
}
