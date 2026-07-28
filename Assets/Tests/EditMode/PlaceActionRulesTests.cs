using NUnit.Framework;

public class PlaceActionRulesTests
{
    // A conquered Town with everything affordable and the action unspent.
    static TownActionSnapshot Town(PlaceType type = PlaceType.Town, bool conquered = true,
        int guardiansRemaining = 0, int influence = 99, int healCost = 3, int crystalCost = 2,
        bool anyUnitAffordable = true, bool visitCanAct = true, bool hasMenu = true)
        => new TownActionSnapshot(type, conquered, guardiansRemaining, influence, healCost,
            crystalCost, anyUnitAffordable, visitCanAct, hasMenu);

    [Test]
    public void Unconquered_AssaultThenMenuOnly()
    {
        var actions = PlaceActionRules.ForTown(Town(conquered: false, guardiansRemaining: 2));
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(PlaceActionId.Assault, actions[0].Id);
        Assert.AreEqual(2, actions[0].CostAmount, "assault badge shows guardians remaining");
        Assert.IsNull(actions[0].CostIcon, "the guardian count is a bare number, not a cost");
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[1].Id);
    }

    [Test]
    public void ConqueredTown_RecruitHealCrystalThenMenu()
    {
        var actions = PlaceActionRules.ForTown(Town());
        Assert.AreEqual(4, actions.Count);
        Assert.AreEqual(PlaceActionId.Recruit, actions[0].Id);
        Assert.AreEqual(PlaceActionId.Heal, actions[1].Id);
        Assert.AreEqual(PlaceActionId.Crystal, actions[2].Id);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[3].Id);
    }

    [Test]
    public void ConqueredCastle_IncludesCardsButDisabled()
    {
        var actions = PlaceActionRules.ForTown(Town(PlaceType.Castle));
        var cards = actions.Find(a => a.Id == PlaceActionId.Cards);
        Assert.AreEqual(PlaceActionId.Cards, cards.Id, "Castle must offer the Cards slot");
        Assert.IsFalse(cards.Enabled, "Cards is an M2 stub and must render locked");
    }

    [Test]
    public void HealShowsItsInfluenceCostAndLocksWhenUnaffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(influence: 2, healCost: 3, crystalCost: 0));
        var heal = actions.Find(a => a.Id == PlaceActionId.Heal);
        Assert.AreEqual(3, heal.CostAmount);
        Assert.AreEqual(IconConcept.Influence, heal.CostIcon);
        Assert.IsFalse(heal.Enabled);
    }

    // The crystal pop-out spends influence with no clamp (Player.Influence), so the
    // slot must carry the same gate the town menu's Crystal button always had.
    [Test]
    public void CrystalShowsItsInfluenceCostAndLocksWhenUnaffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(influence: 1, crystalCost: 2));
        var crystal = actions.Find(a => a.Id == PlaceActionId.Crystal);
        Assert.AreEqual(2, crystal.CostAmount);
        Assert.AreEqual(IconConcept.Influence, crystal.CostIcon);
        Assert.IsFalse(crystal.Enabled);
    }

    [Test]
    public void CrystalUnlocksWhenAffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(influence: 2, crystalCost: 2));
        Assert.IsTrue(actions.Find(a => a.Id == PlaceActionId.Crystal).Enabled);
    }

    [Test]
    public void RecruitLocksWhenNoUnitAffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(anyUnitAffordable: false));
        Assert.IsFalse(actions.Find(a => a.Id == PlaceActionId.Recruit).Enabled);
    }

    [Test]
    public void ActionSpent_ServicesLockButMenuStaysOpen()
    {
        var actions = PlaceActionRules.ForTown(Town(visitCanAct: false));
        foreach (var a in actions)
            if (a.Id != PlaceActionId.OpenMenu)
                Assert.IsFalse(a.Enabled, a.Id + " must lock once the action is spent");
        Assert.IsTrue(actions.Find(a => a.Id == PlaceActionId.OpenMenu).Enabled,
            "the ledger is a free peek and never locks");
    }
}
