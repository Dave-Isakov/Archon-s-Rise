using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArchonsRise.Shrines;

// The shrine engage UI (spec 2026-07-27). A fan of crystal slots arcs over the
// shrine; clicking a slot cycles it through the crystals the player can still
// spare (and back to empty). When every slot is filled a checkmark takes an
// appended fan position and confirms the bargain.
//
// Selection is NON-DESTRUCTIVE: slots hold picks, not crystals. Nothing leaves
// the inventory until Confirm, so dismissing costs nothing and there is no
// refund path to get wrong.
//
// The fan needs no per-hex projection: the player must be STANDING on the shrine
// to open it and the camera rides PlayerPosition, so the shrine is always screen
// centre. Park fanContainer at the authored offset above it and the arc lands.
public class ShrinePanel : MonoBehaviour
{
    [SerializeField] GameObject root;              // panel + fan + click-off catcher
    [SerializeField] CrystalInventory crystals;
    [SerializeField] RectTransform fanContainer;   // parked above screen centre
    [SerializeField] ShrineSlotButton slotPrefab;  // ONE prefab; sprite swapped per bucket
    [Tooltip("Bucket order: Red, Yellow, Green, Purple, Wild.")]
    [SerializeField] Sprite[] bucketSprites;
    [SerializeField] Button confirmButton;         // the checkmark; child of fanContainer
    [SerializeField] CanvasGroup confirmGroup;     // gets the UiLock dim when locked
    [SerializeField] FanSettings fan = new FanSettings
    {
        SpreadDegrees = 0f,   // 0 keeps the crystal icons upright
        CardSpacing = 70f,    // buttons are smaller than cards
        ArcDrop = 22f         // edges sit this far below the centre slots
    };

    readonly List<ShrineSlotButton> slots = new();
    private ShrineToken current;
    private int[] picks;
    private readonly System.Random rng = new System.Random();

    public void Open(ShrineToken token)
    {
        current = token;
        picks = new int[Mathf.Max(0, token.shrineSO.crystalCost)];
        for (int i = 0; i < picks.Length; i++) picks[i] = ShrinePaymentRules.Empty;

        EnsureSlots(picks.Length);
        if (root != null) root.SetActive(true);
        Refresh();
    }

    // Wired to the full-screen click-off catcher behind the fan. Nothing was
    // spent, so this is a plain close — no refund, no action, free peek.
    public void Dismiss() => Close();

    // Wired to each pooled slot's Button via Bind.
    private void OnSlotClicked(int slot)
    {
        if (current == null || picks == null) return;
        picks[slot] = ShrinePaymentRules.NextPick(crystals.ShrinePaymentCounts(), picks, slot);
        Refresh();
    }

    // Wired to the checkmark button's OnClick.
    public void Confirm()
    {
        if (current == null || !ShrinePaymentRules.IsComplete(picks)) return;

        // The button is already gated on this, but a stale click during the same
        // frame the action was spent must not slip through.
        if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.VisitCanAct)
        {
            GameManager.Instance.ValidationMessage("You've already taken your action this turn.");
            return;
        }

        var so = current.shrineSO;
        var cell = current.gridPos;

        // Confirm is the commit point: the action and the crystals go now,
        // whatever the shrine rolls.
        if (TurnPhaseController.Instance != null) TurnPhaseController.Instance.CommitVisitAction();
        crystals.SpendShrinePayment(picks);

        bool good = ShrineRules.IsGood(so.goodRollChance, (float)rng.NextDouble());
        var type = ShrineRules.RollType(so.rewardTypes, max => rng.Next(max));

        // Close before paying out: a good roll can open a card-pick modal and the
        // fan must not sit behind it.
        Close();

        if (good)
        {
            // Safe roll pays 1x now. The bad roll's 2x is paid by CombatController
            // when the guardian dies, not here.
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
        picks = null;
        if (root != null) root.SetActive(false);
    }

    // Grow the pool to `count` and re-bind indices. Slots are reused across
    // visits, so every Open re-points the click handlers.
    private void EnsureSlots(int count)
    {
        if (slotPrefab == null || fanContainer == null) return;
        while (slots.Count < count)
            slots.Add(Instantiate(slotPrefab, fanContainer));

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].gameObject.SetActive(i < count);
            if (i < count) slots[i].Bind(i, OnSlotClicked);
        }
    }

    // Lays the fan out and paints each slot. The checkmark rides an APPENDED fan
    // position, so solving for one extra seat re-centres the arc as it appears —
    // and re-centres back if a slot is cycled to empty again.
    private void Refresh()
    {
        if (current == null || picks == null) return;

        bool complete = ShrinePaymentRules.IsComplete(picks);
        var solved = FanMath.Solve(picks.Length + (complete ? 1 : 0), fan);

        for (int i = 0; i < picks.Length && i < slots.Count; i++)
        {
            Place((RectTransform)slots[i].transform, solved[i]);
            slots[i].Show(SpriteFor(picks[i]));
        }

        if (confirmButton == null) return;
        confirmButton.gameObject.SetActive(complete);
        if (!complete) return;

        Place((RectTransform)confirmButton.transform, solved[picks.Length]);

        // Opening a shrine is a free peek, so a player who already acted this turn
        // can still fill the slots — but the confirm shows locked (UiLock dim +
        // non-interactable), matching how DungeonPanel gates its Delve button.
        bool canAct = TurnPhaseController.Instance == null || TurnPhaseController.Instance.VisitCanAct;
        confirmButton.interactable = canAct;
        UiLock.Apply(confirmGroup, !canAct);
    }

    private static void Place(RectTransform rt, FanSlot slot)
    {
        rt.anchoredPosition = slot.AnchoredPosition;
        rt.localRotation = Quaternion.Euler(0f, 0f, slot.TiltZ);
    }

    private Sprite SpriteFor(int bucket)
        => bucket >= 0 && bucketSprites != null && bucket < bucketSprites.Length
            ? bucketSprites[bucket]
            : null;
}
