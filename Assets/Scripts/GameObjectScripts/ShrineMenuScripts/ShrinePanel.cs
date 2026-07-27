using System;
using UnityEngine;
using UnityEngine.UI;
using ArchonsRise.Shrines;

// The shrine engage UI (spec 2026-07-24, §2). Opens on a live shrine; the player
// places `crystalCost` crystals one at a time via the over-head color buttons,
// then the shrine resolves: safe = 1x instant reward, bad = a tier-3 guardian
// owing 2x. Crystals are spent regardless of the outcome. One-shot.
public class ShrinePanel : MonoBehaviour
{
    // One over-head color button. The panel owns affordability for all of them so
    // the CanPay read lives in exactly one place.
    [Serializable]
    public class ColorButton
    {
        public EmpowerType color;
        public Button button;
        public CanvasGroup group;   // optional: gets the UiLock dim treatment
    }

    [SerializeField] GameObject root;              // the panel + over-head buttons
    [SerializeField] CrystalInventory crystals;
    [SerializeField] GameObject[] slotPips;        // crystalCost slot indicators
    [SerializeField] ColorButton[] colorButtons;

    private ShrineToken current;
    private int placed;
    private readonly System.Random rng = new System.Random();

    public void Open(ShrineToken token)
    {
        current = token;
        placed = 0;
        // Any leftovers from an interrupted flow are not ours to refund.
        crystals.shrineSpentCrystals.Clear();
        if (root != null) root.SetActive(true);
        RefreshSlots();
        RefreshButtonAffordability();
    }

    // Wired to each over-head color button (pass that button's EmpowerType).
    public void PlaceColor(EmpowerType color)
    {
        if (current == null || placed >= current.shrineSO.crystalCost) return;
        if (!crystals.SpendShrineCrystal(color))
        {
            GameManager.Instance.ValidationMessage("You don't have a crystal of that color.");
            return;
        }
        placed++;
        RefreshSlots();
        RefreshButtonAffordability();
        if (placed >= current.shrineSO.crystalCost) Engage();
    }

    // Cancel before the last crystal lands: refund every placed crystal and close.
    // Opening and backing out is a free peek — no action, no crystals.
    public void Cancel()
    {
        if (current == null) return;
        crystals.RefundAllShrineCrystals();
        placed = 0;
        Close();
    }

    private void Engage()
    {
        var so = current.shrineSO;
        var cell = current.gridPos;

        // Spending the action + the crystals is the commit point.
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
        crystals.CommitShrineCrystals();

        bool good = ShrineRules.IsGood(so.goodRollChance, (float)rng.NextDouble());
        var type = ShrineRules.RollType(so.rewardTypes, max => rng.Next(max));

        // Close before paying out: the good roll can open a card-pick modal, and
        // the panel must not sit behind it.
        Close();

        if (good)
        {
            // The safe roll pays 1x now. The bad roll's 2x is paid by
            // CombatController when the guardian dies, not here.
            var rewards = FindAnyObjectByType<Rewards>();
            if (rewards != null) rewards.GrantShrineReward(type, ShrineRules.RewardCount(true), so);
            ShrineTracker.Instance.SetState(cell, ShrineVisualState.ConsumedDormant);
        }
        else
        {
            ShrineTracker.Instance.SpawnGuardian(cell, so, type);
            GameManager.Instance.ValidationMessage("The shrine's bargain turns sour — a guardian rises!");
        }
    }

    private void Close()
    {
        current = null;
        if (root != null) root.SetActive(false);
    }

    private void RefreshSlots()
    {
        if (slotPips == null) return;
        for (int i = 0; i < slotPips.Length; i++)
            if (slotPips[i] != null) slotPips[i].SetActive(i < placed);
    }

    // A color button is live only when the inventory can actually satisfy it (a
    // matching crystal or a wild). Single place that reads CanPay, so the buttons
    // and the UiLock dim never disagree.
    private void RefreshButtonAffordability()
    {
        if (colorButtons == null) return;
        foreach (var cb in colorButtons)
        {
            if (cb == null) continue;
            bool affordable = crystals != null && crystals.CanPay(cb.color);
            if (cb.button != null) cb.button.interactable = affordable;
            UiLock.Apply(cb.group, !affordable);
        }
    }
}
