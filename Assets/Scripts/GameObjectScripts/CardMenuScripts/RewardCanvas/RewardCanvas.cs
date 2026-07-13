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

    private void Choose(CardsSO chosen)
    {
        if (resolved) return;
        resolved = true;
        onChosen?.Invoke(chosen);
        Close();
    }

    // Wired to the Skip button's OnClick.
    public void SkipReward()
    {
        if (resolved) return;
        resolved = true;
        onSkip?.Invoke();
        Close();
    }

    private void Close()
    {
        Clear();
        GameManager.Instance.cardRewardCanvas.enabled = false;
    }

    private void Clear()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }
}
