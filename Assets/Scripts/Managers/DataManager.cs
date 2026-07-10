using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using ArchonsRise.SaveData;

public class DataManager : MonoBehaviour
{
    public SaveFile current = new SaveFile();
    public HashSet<Cell> DefeatedEnemies { get; private set; } = new HashSet<Cell>();
    public string savePath = "";
    public bool IsLoading { get; private set; }
    public int CurrentSeed { get; set; }
    private static DataManager instance;
    public static DataManager Instance { get { return instance; } }

    List<GameObject> playerCardObjects = new();

    // public int playerAttack;
    // public int playerDefend;
    // public int playerInfluence;
    // public int playerExplore;
    // public PlayerSO player;
    // public int playerHandSize;
    // public int improvAttackValue;
    // public int improvDefendValue;
    // public int improvInfluenceValue;
    // public int improvExploreValue;
    // public int playerHP;
    // public int playerExp;
    public CardsSO[] allCards;
    public UnitsSO[] allUnits;
    public SkillsSO[] allSkills;
    public EnemiesSO[] allEnemies;

    public ContentRegistry<CardsSO> Cards { get; private set; }
    public ContentRegistry<UnitsSO> Units { get; private set; }
    public ContentRegistry<SkillsSO> Skills { get; private set; }
    public ContentRegistry<EnemiesSO> Enemies { get; private set; }

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
            BuildRegistries();
        }
        // playerHandSize = player.PlayerHandSize;
        savePath = Application.dataPath + Path.AltDirectorySeparatorChar + "Save.json";
        DontDestroyOnLoad(this.gameObject);
    }

    public bool BuildRegistries()
    {
        try
        {
            Cards = new ContentRegistry<CardsSO>(allCards, c => c.id);
            Units = new ContentRegistry<UnitsSO>(allUnits, u => u.id);
            Skills = new ContentRegistry<SkillsSO>(allSkills, s => s.id);
            Enemies = new ContentRegistry<EnemiesSO>(allEnemies, e => e.id);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Content registry build failed (missing/duplicate id?): {e.Message}");
            return false;
        }
    }

    private void Update() {
        if (!GameControls.Gameplay.Menu.WasPressedThisFrame()) return;

        // A validation message modally captures input; Menu is suppressed so it can't
        // open the main menu over the message (dismiss it with A/B first).
        if (GameManager.Instance.messageCanvas.enabled) return;

        // The run-end screen is terminal; the menu key must not open UI over it.
        if (RunEndController.HasEnded) return;

        // While the pop-out is open, Escape/Start acts as Cancel (closes the
        // pop-out) instead of opening the main menu over it.
        if (InputContextState.Current == InputContext.Inspector)
        {
            FindAnyObjectByType<CardInspector>()?.Close();
            return;
        }
        GameManager.Instance.mainMenuCanvas.enabled = !GameManager.Instance.mainMenuCanvas.enabled;
    }

    // public void CardsOnGameBoardList(GameObject playerCard)
    // {
    //     if(playerCard.activeSelf)
    //     {
    //         playerCardObjects.Add(playerCard);
    //     }
    //     else
    //     {
    //         playerCardObjects.Remove(playerCard);
    //     }
    //     foreach(var card in playerCardObjects)
    //         Debug.Log(card.GetComponent<Card>().cardSO.cardName);
    // }

    // public void AssignPlayerStats(int[] stats)
    // {
    //     playerAttack += stats[0];
    //     playerDefend += stats[1];
    //     playerInfluence += stats[2];
    //     playerExplore += stats[3];
    // }

    // public void UnAssignPlayerStats(int[] stats)
    // {
    //     playerAttack -= stats[0];
    //     playerDefend -= stats[1];
    //     playerInfluence -= stats[2];
    //     playerExplore -= stats[3];
    // }

    public void NewGame()
    {
        // A button may target an inactive/duplicate DataManager; route to the active singleton.
        if (instance != null && instance != this) { instance.NewGame(); return; }
        IsLoading = false;
        CurrentSeed = new System.Random().Next(int.MinValue, int.MaxValue);
        DefeatedEnemies = new HashSet<Cell>();
        SceneManager.LoadScene(1);
    }

    public void LoadGame()
    {
        // A button may target an inactive/duplicate DataManager (its Awake never ran and
        // StartCoroutine fails on it); route to the active singleton.
        if (instance != null && instance != this) { instance.LoadGame(); return; }
        savePath = Application.dataPath + Path.AltDirectorySeparatorChar + "Save.json";
        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"No save file found at {savePath}");
            return;
        }

        using (StreamReader reader = new(savePath))
            current = SaveMigrator.Migrate(SaveSerializer.FromJson(reader.ReadToEnd()));

        CurrentSeed = current.run.map.seed;
        DefeatedEnemies = new HashSet<Cell>(current.run.map.defeatedEnemies);

        IsLoading = true;
        SceneManager.sceneLoaded += OnGameSceneLoaded;
        SceneManager.LoadScene(1);
    }

    private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnGameSceneLoaded;
        StartCoroutine(RestoreAfterSceneInit());
    }

    private IEnumerator RestoreAfterSceneInit()
    {
        // sceneLoaded fires before Start(); wait one frame so GridGeneration.Start has
        // generated the map and spawned EnemyTokens (gridPos set in their Start), and
        // PlayerHand.Start has skipped its default draw while IsLoading is still true.
        yield return null;

        // try/finally guarantees IsLoading is cleared even if restore throws, so a failed
        // load (e.g. a bad card id or missing prefab) can never permanently block saving.
        try { RestoreNow(); }
        finally { IsLoading = false; }
    }

    private void RestoreNow()
    {
        var player   = FindAnyObjectByType<Player>();
        var pos      = FindAnyObjectByType<PlayerPosition>();
        var deck     = FindAnyObjectByType<PlayerDeck>();
        var hand     = FindAnyObjectByType<PlayerHand>();
        var discard  = FindAnyObjectByType<DiscardPile>();
        var crystals = FindAnyObjectByType<CrystalInventory>();
        var game     = GameManager.Instance;

        if (player == null || pos == null || deck == null || hand == null)
        {
            Debug.LogError("Loaded scene missing core objects; cannot restore save.");
            return;
        }

        var run = current.run;

        // Remove enemy tokens whose cell was recorded as defeated.
        foreach (var token in FindObjectsByType<EnemyToken>())
            if (MapDelta.IsDefeated(DefeatedEnemies, new Cell(token.gridPos.x, token.gridPos.y)))
                Destroy(token.gameObject);

        // Re-apply guardian-conquest progress to the regenerated places. The
        // town tokens registered themselves (rosterSize/type) during their
        // Start; the ledger tolerates either order regardless.
        ConquestTracker.Instance.ApplySave(run.places);

        // Re-clear fog at the cells the player had already revealed.
        var dir = FindAnyObjectByType<DirectionButton>();
        if (dir != null && dir.Fog != null)
            foreach (var c in run.map.revealedCells)
                dir.Fog.SetTile(new Vector3Int(c.x, c.y, 0), null);

        // Restore ExpToNextLevel first so Update() doesn't fire a spurious level-up.
        player.ExpToNextLevel  = run.player.expToNextLevel;
        player.PlayerHP        = run.player.hp;
        player.PlayerLevel     = run.player.level;
        player.PlayerExp       = run.player.exp;
        player.PlayerAttack    = run.player.attack;
        player.PlayerDefend    = run.player.defend;
        player.PlayerInfluence = run.player.influence;
        player.PlayerExplore   = run.player.explore;
        pos.transform.position = new Vector3(run.player.position[0], run.player.position[1], run.player.position[2]);

        if (crystals != null) crystals.SetCounts(run.crystalCounts);

        deck.RebuildDeck(Cards.Resolve(run.deckCardIds));
        hand.RebuildHand(Cards.Resolve(run.handCardIds));
        if (discard != null) discard.RebuildDiscard(Cards.Resolve(run.discardCardIds));

        if (game != null) { game.Round = run.round; game.Turn = run.turn; }
        if (DoomClock.Instance != null) DoomClock.Instance.SetLoaded(run.doom);
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.RoundsSinceSpawn = run.roundsSinceSpawn;
            EnemySpawner.Instance.RestoreSpawned(run.spawnedEnemies, Enemies);
        }

        player.RebuildUnits(Units.Resolve(run.unitIds), run.unitExhausted);
        player.RebuildSkills(Skills.Resolve(run.player.ownedSkillIds),
            new HashSet<string>(run.player.exhaustedSkillIds));
    }

    public SaveFile CaptureRunState()
    {
        var player    = FindAnyObjectByType<Player>();
        var pos       = FindAnyObjectByType<PlayerPosition>();

        if (player == null)
        {
            Debug.LogWarning("CaptureRunState: no Player in scene; skipping capture.");
            return null;
        }
        var deck      = FindAnyObjectByType<PlayerDeck>();
        var hand      = FindAnyObjectByType<PlayerHand>();
        var discard   = FindAnyObjectByType<DiscardPile>();
        var crystals  = FindAnyObjectByType<CrystalInventory>();
        var game      = GameManager.Instance;

        var file = new SaveFile { schemaVersion = 5 };
        var run  = file.run;

        run.player.hp            = player.PlayerHP;
        run.player.level         = player.PlayerLevel;
        run.player.exp           = player.PlayerExp;
        run.player.expToNextLevel = player.ExpToNextLevel;
        run.player.attack        = player.PlayerAttack;
        run.player.defend        = player.PlayerDefend;
        run.player.influence     = player.PlayerInfluence;
        run.player.explore       = player.PlayerExplore;
        run.player.position      = pos != null
            ? new[] { pos.transform.position.x, pos.transform.position.y, pos.transform.position.z }
            : new float[3];

        run.crystalCounts  = crystals != null ? crystals.GetCounts() : System.Array.Empty<int>();
        run.deckCardIds    = CardIds(deck != null ? deck.CardsInDeck : new List<Card>());
        run.handCardIds    = CardIds(hand != null ? hand.cardsInPlay : new List<Card>());
        run.discardCardIds = DiscardIds(discard);
        // Single-source capture so unitIds[i] and unitExhausted[i] always pair:
        // both come from the same Unit-object iteration.
        var unitObjs = FindObjectsByType<Unit>();
        run.unitIds       = System.Array.ConvertAll(unitObjs, u => u.unitSO.id);
        run.unitExhausted = System.Array.ConvertAll(unitObjs, u => u.IsPlayed);
        run.player.ownedSkillIds     = SkillIds(player);
        run.player.exhaustedSkillIds = ExhaustedSkillIds();

        run.map.seed            = CurrentSeed;
        run.map.defeatedEnemies = MapDelta.ToArray(DefeatedEnemies);
        run.places = ConquestTracker.Instance.ExportPlaces();

        var dir = FindAnyObjectByType<DirectionButton>();
        if (dir != null && dir.Fog != null)
            run.map.revealedCells = CaptureRevealedCells(dir.Fog);

        run.round = game != null ? game.Round : 0;
        run.turn  = game != null ? game.Turn  : 0;
        run.doom  = DoomClock.Instance != null ? DoomClock.Instance.Doom : 0;
        run.roundsSinceSpawn = EnemySpawner.Instance != null ? EnemySpawner.Instance.RoundsSinceSpawn : 0;
        run.spawnedEnemies   = EnemySpawner.Instance != null ? EnemySpawner.Instance.ExportAlive()
                                                             : System.Array.Empty<SpawnedEnemy>();

        return file;
    }

    private static string[] CardIds(List<Card> cards)
    {
        var ids = new List<string>(cards.Count);
        foreach (var c in cards)
            if (c != null && c.cardSO != null) ids.Add(c.cardSO.id);
        return ids.ToArray();
    }

    private static string[] DiscardIds(DiscardPile discard)
    {
        if (discard == null) return System.Array.Empty<string>();
        return CardIds(discard.Cards);
    }

    private static string[] SkillIds(Player player)
    {
        var ids = new List<string>();
        if (player == null) return ids.ToArray();
        foreach (var s in player.Skills)
            if (s != null) ids.Add(s.id);
        return ids.ToArray();
    }

    // Exhaust state lives on the tokens (mirrors how unit exhaustion lives on
    // Unit); saving happens only at settled states, so tokens are authoritative.
    private static string[] ExhaustedSkillIds()
    {
        var ids = new List<string>();
        foreach (var token in FindObjectsByType<SkillToken>())
            if (token.IsUsed && token.skillSO != null) ids.Add(token.skillSO.id);
        return ids.ToArray();
    }

    // A revealed cell is any playable cell the fog no longer covers (any terrain). Reveal is
    // monotonic, so re-clearing this set on load reproduces the player's explored area.
    private static Cell[] CaptureRevealedCells(Tilemap fog)
    {
        var revealed = new List<Cell>();
        for (int x = 0; x < 20; x++)        // map is generated over a 20x20 grid (GridGeneration)
            for (int y = 0; y < 20; y++)
                if (!fog.HasTile(new Vector3Int(x, y, 0)))
                    revealed.Add(new Cell(x, y));
        return revealed.ToArray();
    }

    public bool IsSettledState()
    {
        var game = GameManager.Instance;
        if (game == null) return false;
        if (IsLoading) return false;

        // A finished run must never be re-saved (the run-end screen deleted it).
        if (RunEndController.HasEnded) return false;

        // No modal sub-screen open.
        if (game.combatCanvas != null && game.combatCanvas.enabled) return false;
        if (game.townCanvas != null && game.townCanvas.enabled) return false;
        if (game.cardRewardCanvas != null && game.cardRewardCanvas.enabled) return false;
        if (game.cardListCanvas != null && game.cardListCanvas.enabled) return false;

        // No level-up payout mid-flight (skill modal open or picks queued).
        var levelUp = FindAnyObjectByType<LevelUpController>();
        if (levelUp != null && levelUp.Busy) return false;
        var levelUpModal = FindAnyObjectByType<LevelUpModal>();
        if (levelUpModal != null && levelUpModal.IsOpen) return false;

        // Undo/command stack empty (no card mid-play).
        if (game.commands != null && !game.commands.IsEmpty) return false;

        return true;
    }

    public void SaveGame()
    {
        // A button may target an inactive/duplicate DataManager (savePath unset); route to the active singleton.
        if (instance != null && instance != this) { instance.SaveGame(); return; }
        if (!IsSettledState())
        {
            Debug.Log("Save skipped: not at a settled state.");
            return;
        }
        var captured = CaptureRunState();
        if (captured == null)
        {
            Debug.LogWarning("Save skipped: run state could not be captured.");
            return;
        }
        current = captured;
        string json = SaveSerializer.ToJson(current);
        savePath = Application.dataPath + Path.AltDirectorySeparatorChar + "Save.json";
        Debug.Log($"Saving data at {savePath}");
        using StreamWriter writer = new StreamWriter(savePath);
        writer.Write(json);
    }

    // Called by RunEndController when a run ends: the save must not be resumable.
    public void DeleteSave()
    {
        if (instance != null && instance != this) { instance.DeleteSave(); return; }
        savePath = Application.dataPath + Path.AltDirectorySeparatorChar + "Save.json";
        if (File.Exists(savePath)) File.Delete(savePath);
        current = new SaveFile();
    }

    public void Quit()
    {
        Application.Quit();
    }
}
