using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArchonsRise.SaveData;

public class DataManager : MonoBehaviour
{
    public PlayerData playerData;
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

    public ContentRegistry<CardsSO> Cards { get; private set; }
    public ContentRegistry<UnitsSO> Units { get; private set; }

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
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Content registry build failed (missing/duplicate id?): {e.Message}");
            return false;
        }
    }

    private void Update() {
        if(Input.GetKeyDown(KeyCode.Escape) && !GameManager.Instance.mainMenuCanvas.enabled)
        {
            GameManager.Instance.mainMenuCanvas.enabled = true;
        }
        else if(Input.GetKeyDown(KeyCode.Escape) && GameManager.Instance.mainMenuCanvas.enabled)
        {
            GameManager.Instance.mainMenuCanvas.enabled = false;
        }
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
        CurrentSeed = new System.Random().Next(int.MinValue, int.MaxValue);
        SceneManager.LoadScene(1);
    }

    public void LoadGame()
    {
        if(!File.Exists(savePath))
        {

            Debug.LogWarning($"No save file found at {savePath}");
            return;
        }

        using (StreamReader reader = new(savePath))
        {
            playerData = JsonUtility.FromJson<PlayerData>(reader.ReadToEnd());
        }

        // The player/position objects don't exist until the gameplay scene finishes
        // loading, so defer restoring their state to the sceneLoaded callback.
        IsLoading = true;
        SceneManager.sceneLoaded += OnGameSceneLoaded;
        SceneManager.LoadScene(1);
    }

    private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnGameSceneLoaded;

        var player = FindAnyObjectByType<Player>();
        var playerPosition = FindAnyObjectByType<PlayerPosition>();
        if(player == null || playerPosition == null)
        {
            Debug.LogError("Loaded scene is missing Player or PlayerPosition; cannot restore save.");
            IsLoading = false;
            return;
        }

        player.PlayerAttack = playerData.playerAttack;
        player.PlayerDefend = playerData.playerDefend;
        player.PlayerInfluence = playerData.playerInfluence;
        player.PlayerExplore = playerData.playerExplore;
        player.PlayerExp = playerData.playerExp;
        player.PlayerHandSize = playerData.playerHandSize;
        player.PlayerLevel = playerData.playerLevel;
        playerPosition.transform.position = new Vector3(playerData.position[0], playerData.position[1], playerData.position[2]);

        IsLoading = false;
    }

    public SaveFile CaptureRunState()
    {
        var player    = FindAnyObjectByType<Player>();
        var pos       = FindAnyObjectByType<PlayerPosition>();
        var deck      = FindAnyObjectByType<PlayerDeck>();
        var hand      = FindAnyObjectByType<PlayerHand>();
        var discard   = FindAnyObjectByType<DiscardPile>();
        var crystals  = FindAnyObjectByType<CrystalInventory>();
        var game      = GameManager.Instance;

        var file = new SaveFile { schemaVersion = 1 };
        var run  = file.run;

        run.player.hp            = player.PlayerHP;
        run.player.handSize      = player.PlayerHandSize;
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
        run.unitIds        = UnitIds(player);

        run.map.seed            = CurrentSeed;
        run.map.defeatedEnemies = MapDelta.ToArray(DefeatedEnemies);

        run.round = game != null ? game.Round : 0;
        run.turn  = game != null ? game.Turn  : 0;

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

    private static string[] UnitIds(Player player)
    {
        var ids = new List<string>();
        if (player == null) return ids.ToArray();
        foreach (var u in player.Units)
            if (u != null) ids.Add(u.id);
        return ids.ToArray();
    }

    public void SaveGame()
    {
        current = CaptureRunState();
        string json = SaveSerializer.ToJson(current);
        Debug.Log($"Saving data at {savePath}");
        using StreamWriter writer = new StreamWriter(savePath);
        writer.Write(json);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
