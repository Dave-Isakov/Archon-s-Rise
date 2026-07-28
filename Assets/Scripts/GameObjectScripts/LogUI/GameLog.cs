using System;
using UnityEngine;

// The one entry point for player-facing messages (spec 2026-07-28). Replaces
// GameManager.ValidationMessage and its blocking canvas: Post appends to the
// history AND raises a toast, and never blocks input.
//
// Lazily creates its own scene GameObject (the RewardQueue / ConquestTracker
// pattern) so no scene wiring is required; being scene-scoped means a new run
// starts with an empty log.
public class GameLog : MonoBehaviour
{
    private static GameLog instance;
    public static GameLog Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("GameLog").AddComponent<GameLog>();
            return instance;
        }
    }

    // The instance if one already exists, else null — NEVER creates. Teardown
    // paths (OnDisable, OnDestroy) must use this: touching Instance while the
    // scene is being destroyed would resurrect the singleton and Unity errors on
    // creating a GameObject at that point.
    public static GameLog Existing { get { return instance; } }

    public PlayerLogCore Log { get; } = new PlayerLogCore();

    // Raised with the text of each new entry. ToastRail subscribes; LogPanel
    // reads Log directly when it opens.
    public event Action<string> Posted;

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Post(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        // The run-end screen is terminal: nothing may appear over it. Same guard
        // ValidationMessage carried.
        if (RunEndController.HasEnded) return;

        int day = GameManager.Instance != null ? GameManager.Instance.Round : 0;
        Log.Append(day, message);
        if (Posted != null) Posted(message);
    }
}
