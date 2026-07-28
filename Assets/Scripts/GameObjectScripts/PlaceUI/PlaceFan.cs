using System.Collections.Generic;
using UnityEngine;

// What a place must provide to be shown on the fan. Implemented by
// PlaceTokenBase subclasses; PlaceFan never learns about place types, which is
// what lets a new place type land without touching this file.
public interface IPlaceFanHost
{
    List<PlaceAction> BuildActions();
    void Dispatch(PlaceActionId id);
}

// The arc of action icons over the player's head (spec 2026-07-28). Replaces
// the full-screen town/dungeon canvases as the default way to interact with a
// place; the full menus live behind the ledger slot.
//
// The container is parked at an authored offset above screen centre with no
// per-hex projection: a place is entered by STANDING on it and the camera rides
// PlayerPosition, so the place is always screen centre (same reasoning as
// ShrinePanel's fan).
public class PlaceFan : MonoBehaviour
{
    [SerializeField] GameObject root;              // fan + click-off catcher
    [SerializeField] RectTransform fanContainer;   // parked above screen centre
    [SerializeField] PlaceFanSlot slotPrefab;      // ONE prefab; glyph swapped per action
    [SerializeField] ClickOffCatcher catcher;
    [SerializeField] FanSettings fan = new FanSettings
    {
        SpreadDegrees = 0f,   // 0 keeps the action icons upright
        CardSpacing = 70f,    // buttons are smaller than cards
        ArcDrop = 22f,        // edges sit this far below the centre slots
    };

    readonly List<PlaceFanSlot> slots = new();
    readonly List<RectTransform> seats = new();
    IPlaceFanHost current;
    List<PlaceAction> shown;

    static PlaceFan instance;
    public static PlaceFan Instance
        => instance != null
            ? instance
            : (instance = FindAnyObjectByType<PlaceFan>(FindObjectsInactive.Include));

    public bool IsOpen => current != null;

    // Read-only access for hover triggers that need to know which place is open.
    public IPlaceFanHost CurrentHost => current;

    public void Open(IPlaceFanHost host)
    {
        current = host;
        if (root != null) root.SetActive(true);
        if (catcher != null) catcher.SetArmed(true);
        shown = null;      // force the first Render
        Refresh();
    }

    // Wired to the click-off catcher. Nothing was spent, so this is a plain
    // close — opening a place is a free peek.
    public void Dismiss()
    {
        current = null;
        shown = null;
        if (catcher != null) catcher.SetArmed(false);
        if (root != null) root.SetActive(false);
    }

    void Update()
    {
        if (current != null) Refresh();
    }

    // Rebuild the action list every frame but re-render ONLY when it changed, so
    // Delve unlocks the instant an Explore card is played and Recruit locks when
    // influence drops — with no per-frame layout cost and no event wiring. This
    // replaces the five per-button Update() loops the TownButtons used.
    void Refresh()
    {
        var next = current.BuildActions();
        if (Same(shown, next)) return;
        shown = next;
        Render(next);
    }

    static bool Same(List<PlaceAction> a, List<PlaceAction> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id) return false;
            if (a[i].Enabled != b[i].Enabled) return false;
            if (a[i].CostAmount != b[i].CostAmount) return false;
        }
        return true;
    }

    void Render(List<PlaceAction> actions)
    {
        EnsureSlots(actions.Count);
        // EnsureSlots bails when the prefab or container is unwired; without this
        // the loop below would index an empty pool.
        if (slots.Count < actions.Count) return;

        seats.Clear();
        for (int i = 0; i < actions.Count; i++)
        {
            slots[i].Bind(actions[i], OnSlotClicked);
            seats.Add((RectTransform)slots[i].transform);
        }
        FanLayout.Place(seats, fan);
    }

    // Grow the pool to `count` and hide the rest. Slots are reused across visits.
    void EnsureSlots(int count)
    {
        if (slotPrefab == null || fanContainer == null) return;
        while (slots.Count < count)
            slots.Add(Instantiate(slotPrefab, fanContainer));
        for (int i = 0; i < slots.Count; i++)
            slots[i].gameObject.SetActive(i < count);
    }

    void OnSlotClicked(PlaceActionId id)
    {
        if (current == null) return;
        var host = current;
        // Close first: a dispatch can open a panel or a modal, and the fan must
        // not sit behind it. The ledger slot re-opens its own menu.
        Dismiss();
        host.Dispatch(id);
    }
}
