using NUnit.Framework;

public class PlaceActionRulesTests
{
    // A conquered Town with everything affordable and the action unspent.
    static TownActionSnapshot Town(PlaceType type = PlaceType.Town, bool conquered = true,
        int guardiansRemaining = 0, int influence = 99, int healCost = 3, int crystalCost = 2,
        bool sellsCards = false, int cardCost = 5,
        bool anyUnitAffordable = true, bool visitCanAct = true, bool hasMenu = true)
        => new TownActionSnapshot(type, conquered, guardiansRemaining, influence, healCost,
            crystalCost, sellsCards, cardCost, anyUnitAffordable, visitCanAct, hasMenu);

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
    public void SellingTown_ShowsCardsBetweenHealAndCrystal()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true));
        Assert.AreEqual(5, actions.Count);
        Assert.AreEqual(PlaceActionId.Recruit, actions[0].Id);
        Assert.AreEqual(PlaceActionId.Heal, actions[1].Id);
        Assert.AreEqual(PlaceActionId.Cards, actions[2].Id);
        Assert.AreEqual(PlaceActionId.Crystal, actions[3].Id);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[4].Id);
    }

    [Test]
    public void CardsShowsItsInfluenceCostAndUnlocksWhenAffordable()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, influence: 5, cardCost: 5));
        var cards = actions.Find(a => a.Id == PlaceActionId.Cards);
        Assert.AreEqual(IconConcept.Card, cards.Icon);
        Assert.AreEqual(IconConcept.Influence, cards.CostIcon);
        Assert.AreEqual(5, cards.CostAmount);
        Assert.IsTrue(cards.Enabled);
    }

    [Test]
    public void CardsLocksBelowItsCost()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, influence: 4, cardCost: 5));
        Assert.IsFalse(actions.Find(a => a.Id == PlaceActionId.Cards).Enabled);
    }

    [Test]
    public void CardsLocksOnceTheActionIsSpent()
    {
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, visitCanAct: false));
        Assert.IsFalse(actions.Find(a => a.Id == PlaceActionId.Cards).Enabled);
    }

    [Test]
    public void NoCardList_NoCardsSlot()
    {
        foreach (PlaceType type in System.Enum.GetValues(typeof(PlaceType)))
        {
            var actions = PlaceActionRules.ForTown(Town(type, sellsCards: false));
            Assert.AreEqual(0, actions.FindAll(a => a.Id == PlaceActionId.Cards).Count,
                type + " must not offer Cards without a card list");
        }
    }

    [Test]
    public void AnyPlaceTypeWithAListSellsCards()
    {
        foreach (PlaceType type in System.Enum.GetValues(typeof(PlaceType)))
        {
            var actions = PlaceActionRules.ForTown(Town(type, sellsCards: true));
            Assert.AreEqual(1, actions.FindAll(a => a.Id == PlaceActionId.Cards).Count,
                type + " must offer Cards when it has a card list");
        }
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
        var actions = PlaceActionRules.ForTown(Town(sellsCards: true, visitCanAct: false));
        foreach (var a in actions)
            if (a.Id != PlaceActionId.OpenMenu)
                Assert.IsFalse(a.Enabled, a.Id + " must lock once the action is spent");
        Assert.IsTrue(actions.Find(a => a.Id == PlaceActionId.OpenMenu).Enabled,
            "the ledger is a free peek and never locks");
    }

    [Test]
    public void Dungeon_DelveShowsExploreCostThenMenu()
    {
        var actions = PlaceActionRules.ForDungeon(
            new DungeonActionSnapshot(false, 5, 2, true, true));
        Assert.AreEqual(2, actions.Count);
        Assert.AreEqual(PlaceActionId.Delve, actions[0].Id);
        Assert.AreEqual(IconConcept.Explore, actions[0].CostIcon);
        Assert.AreEqual(2, actions[0].CostAmount);
        Assert.IsTrue(actions[0].Enabled);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[1].Id);
    }

    [Test]
    public void Dungeon_DelveLocksBelowExploreCost()
    {
        var actions = PlaceActionRules.ForDungeon(
            new DungeonActionSnapshot(false, 1, 2, true, true));
        Assert.IsFalse(actions[0].Enabled);
    }

    [Test]
    public void Dungeon_DelveLocksOnceTheActionIsSpent()
    {
        var actions = PlaceActionRules.ForDungeon(
            new DungeonActionSnapshot(false, 9, 2, false, true));
        Assert.IsFalse(actions[0].Enabled);
        Assert.IsTrue(actions[1].Enabled, "the ledger is a free peek and never locks");
    }

    [Test]
    public void Dungeon_CompleteLeavesMenuOnly()
    {
        var actions = PlaceActionRules.ForDungeon(
            new DungeonActionSnapshot(true, 9, 2, true, true));
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(PlaceActionId.OpenMenu, actions[0].Id);
    }

    [Test]
    public void LiveShrine_EngageOnlyWithNoLedgerSlot()
    {
        var actions = PlaceActionRules.ForShrine(
            new ShrineActionSnapshot(isLive: true, isGuarded: false, crystalCost: 4, visitCanAct: true));
        Assert.AreEqual(1, actions.Count, "a shrine has no detail menu, so no ledger slot");
        Assert.AreEqual(PlaceActionId.Engage, actions[0].Id);
        Assert.AreEqual(IconConcept.Crystal, actions[0].CostIcon);
        Assert.AreEqual(4, actions[0].CostAmount);
        Assert.IsTrue(actions[0].Enabled);
    }

    [Test]
    public void SpentShrine_EngagePresentButDisabled()
    {
        var actions = PlaceActionRules.ForShrine(
            new ShrineActionSnapshot(isLive: false, isGuarded: false, crystalCost: 4, visitCanAct: true));
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(PlaceActionId.Engage, actions[0].Id);
        Assert.IsFalse(actions[0].Enabled,
            "a spent shrine shows a locked slot, never a message");
    }

    [Test]
    public void Shrine_EngageLocksOnceTheActionIsSpent()
    {
        var actions = PlaceActionRules.ForShrine(
            new ShrineActionSnapshot(isLive: true, isGuarded: false, crystalCost: 4, visitCanAct: false));
        Assert.IsFalse(actions[0].Enabled);
    }

    [Test]
    public void GuardedShrine_ShowsAssaultWithNoCrystalCost()
    {
        var actions = PlaceActionRules.ForShrine(
            new ShrineActionSnapshot(isLive: false, isGuarded: true, crystalCost: 4, visitCanAct: true));
        Assert.AreEqual(1, actions.Count);
        Assert.AreEqual(PlaceActionId.Assault, actions[0].Id);
        Assert.AreEqual(IconConcept.Attack, actions[0].Icon);
        Assert.AreEqual(0, actions[0].CostAmount, "the crystals were paid at the bargain, not again");
    }

    [Test]
    public void GuardedShrine_AssaultStaysEnabledWithTheActionSpent()
    {
        var actions = PlaceActionRules.ForShrine(
            new ShrineActionSnapshot(isLive: false, isGuarded: true, crystalCost: 4, visitCanAct: false));
        Assert.IsTrue(actions[0].Enabled,
            "a spent action opens the guardian preview-only; it never hides the slot");
    }
}
