using System;
using System.Collections.Generic;
using UnityEngine;

// Owns the lifecycle of the "pick one of N cards" reward screen. It spawns
// display-only previews, guards against double-resolution, and reports the
// player's choice (or skip) via callbacks. It never mutates the deck itself.
public class RewardCanvas : MonoBehaviour
{
    [SerializeField] GameObject[] cardLocations = new GameObject[3];
    [SerializeField] GameObject rewardCardPrefab; // root has a CardPreview
    private readonly List<GameObject> spawned = new();
    private bool resolved;
    private Action<CardsSO> onChosen;
    private Action onSkip;

    public void Offer(IReadOnlyList<CardsSO> candidates, Action<CardsSO> onChosen, Action onSkip)
    {
        if (GameManager.Instance.cardRewardCanvas.enabled)
            Debug.LogError("RewardCanvas.Offer: reward canvas already open — modal routing bug (route via RewardQueue).");
        Clear();
        resolved = false;
        this.onChosen = onChosen;
        this.onSkip = onSkip;

        GameManager.Instance.cardRewardCanvas.enabled = true;

        for (int i = 0; i < cardLocations.Length && i < candidates.Count; i++)
        {
            var preview = Instantiate(rewardCardPrefab, cardLocations[i].transform, false);
            preview.transform.localScale = new Vector3(3, 3, 3);
            preview.GetComponent<CardPreview>().Bind(candidates[i], Choose);
            spawned.Add(preview);
        }
    }

    // CLOSE BEFORE NOTIFYING, always (mirrors LevelUpModal.Choose). The callback
    // ends with the RewardQueue's done(), and the queue advances synchronously
    // inside it — so the NEXT card pick calls Offer() before this frame unwinds.
    // Closing afterwards tore that successor straight back down: its previews
    // destroyed, its canvas disabled, its done() never called, which also wedged
    // the queue for the rest of the run. That is why a 2-card shrine payout only
    // ever let you pick once (2026-07-31).
    //
    // The callback is captured first so Close() can clear the fields it reads.
    private void Choose(CardsSO chosen)
    {
        if (resolved) return;
        resolved = true;
        var callback = onChosen;
        Close();
        callback?.Invoke(chosen);
    }

    // Wired to the Skip button's OnClick.
    public void SkipReward()
    {
        if (resolved) return;
        resolved = true;
        var callback = onSkip;
        Close();
        callback?.Invoke();
    }

    private void Close()
    {
        Clear();
        onChosen = null;
        onSkip = null;
        GameManager.Instance.cardRewardCanvas.enabled = false;
    }

    private void Clear()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }
}
