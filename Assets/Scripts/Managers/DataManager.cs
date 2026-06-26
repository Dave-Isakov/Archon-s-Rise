using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArchonsRise.SaveData;

public class DataManager : MonoBehaviour
{
    public PlayerData playerData;
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

    public void SaveGame()
    {
        string path = savePath;
        Debug.Log($"Saving data at {path}");
        string json = JsonUtility.ToJson(playerData);
        print(json);

        using StreamWriter writer = new StreamWriter(path);
        writer.Write(json);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
