#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchonsRise.SaveData;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Dev tool: while in Play Mode on a fresh New Game (GameBoard), mutates the live
// session into a late-game state and writes Save.json, so victory conditions can
// be tested without grinding there. Editor-only; never shipped. All content it
// grants is pulled from the baked content registry, so the save reloads cleanly.
// Tweak the constants below to taste.
public static class LateGameSaveTool
{
    const int Level           = 7;
    const int PlayerHp        = 5;
    const int StartingDoom    = 8;    // mid band: tier-2 enemies + stat scaling active
    const int Round           = 10;
    const int UnitCount       = 2;
    const int SkillCount      = 3;
    const int TotalCards      = 16;   // split into hand (PlayerHandSize) + deck
    static readonly Vector3Int MapCenter = new Vector3Int(10, 10, 0);
    const int RevealRadius    = 3;    // fog cleared around the placed player

    [MenuItem("Tools/Archon's Rise/Create Late-Game Test Save")]
    public static void Create()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Late-Game Test Save",
                "Enter Play Mode on a fresh New Game (GameBoard scene) first, then run this again.", "OK");
            return;
        }

        var dm      = DataManager.Instance;
        var player  = Object.FindAnyObjectByType<Player>();
        var pos     = Object.FindAnyObjectByType<PlayerPosition>();
        var deck    = Object.FindAnyObjectByType<PlayerDeck>();
        var hand    = Object.FindAnyObjectByType<PlayerHand>();
        var grid    = Object.FindAnyObjectByType<Grid>();
        var dir     = Object.FindAnyObjectByType<DirectionButton>();
        var tracker = ConquestTracker.Instance;

        if (dm == null || player == null || pos == null || deck == null || hand == null || grid == null)
        {
            Debug.LogError("Late-Game Test Save: missing core objects — run this in the GameBoard play session.");
            return;
        }

        // --- Level / vitals (ExpToNextLevel high so Update() won't fire a level-up) ---
        player.PlayerLevel    = Level;
        player.PlayerHP       = PlayerHp;
        player.PlayerExp      = 0;
        player.ExpToNextLevel = 9999;

        // --- Units, skills, deck: everything from the baked registry so it reloads ---
        var units = (dm.allUnits ?? new UnitsSO[0]).Where(u => u != null).Take(UnitCount).ToList();
        player.RebuildUnits(units);

        var skills = (dm.allSkills ?? new SkillsSO[0]).Where(s => s != null).Take(SkillCount).ToList();
        player.RebuildSkills(skills, new HashSet<string>());

        var pool = (dm.allCards ?? new CardsSO[0])
            .Where(c => c != null && c.cardType != StatType.Wound).ToList();
        var cards = new List<CardsSO>();
        for (int i = 0; i < TotalCards && pool.Count > 0; i++) cards.Add(pool[i % pool.Count]);
        int handSize = Mathf.Max(1, player.PlayerHandSize);
        deck.RebuildDeck(cards.Skip(handSize).ToList());
        hand.RebuildHand(cards.Take(handSize).ToList());

        // --- Move to the map centre (nearest walkable cell) and clear fog around it ---
        var walk = dir != null ? dir.Map : null;
        var center = NearestWalkable(walk, MapCenter);
        pos.transform.position = grid.CellToWorld(center);
        if (dir != null && dir.Fog != null)
            for (int dx = -RevealRadius; dx <= RevealRadius; dx++)
                for (int dy = -RevealRadius; dy <= RevealRadius; dy++)
                    dir.Fog.SetTile(new Vector3Int(center.x + dx, center.y + dy, 0), null);

        // --- Doom + round ---
        if (DoomClock.Instance != null) DoomClock.Instance.SetLoaded(StartingDoom);
        if (GameManager.Instance != null) GameManager.Instance.Round = Round;

        // --- Pre-conquer one Castle so a single assault wins the run ---
        string conquered = "none";
        var castle = Object.FindObjectsByType<TownToken>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.townSO != null && t.townSO.placeType == PlaceType.Castle);
        if (castle != null && tracker != null)
        {
            int roster = castle.townSO.guardians != null ? castle.townSO.guardians.Count : 0;
            tracker.Register(castle.gridPos, PlaceType.Castle, roster);
            for (int i = 0; i < roster; i++) tracker.RecordDefeat(castle.gridPos);
            conquered = $"{castle.townSO.cardName} @ {castle.gridPos} ({roster} guardians)";
        }
        else Debug.LogWarning("Late-Game Test Save: no Castle found to pre-conquer this seed.");

        // --- Capture the live state and write Save.json (bypasses the settled-state gate) ---
        var file = dm.CaptureRunState();
        if (file == null) { Debug.LogError("Late-Game Test Save: CaptureRunState returned null."); return; }
        dm.current = file;
        string path = Application.dataPath + Path.AltDirectorySeparatorChar + "Save.json";
        File.WriteAllText(path, SaveSerializer.ToJson(file));

        Debug.Log($"Late-Game Test Save written to {path}\n" +
                  $"level {Level}, {units.Count} units, {skills.Count} skills, {cards.Count} cards, " +
                  $"doom {StartingDoom}, centred at {center}, castle pre-conquered: {conquered}. " +
                  $"Load Game to reuse it.");
    }

    // Nearest cell to `target` that has a walkable tile (spiral by Chebyshev ring).
    static Vector3Int NearestWalkable(Tilemap map, Vector3Int target)
    {
        if (map == null) return target;
        for (int r = 0; r < 20; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = new Vector3Int(target.x + dx, target.y + dy, 0);
                    if (map.HasTile(c)) return c;
                }
        return target;
    }
}
#endif
