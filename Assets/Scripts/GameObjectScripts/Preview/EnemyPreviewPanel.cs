using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The preview view. Input-agnostic: it shows a list of enemies at a screen
// position, or one whole-panel "blind" message when any enemy is hidden. Mouse
// hover drives it today; a gamepad focus will drive the same Show/Hide later.
// Scene-placed singleton (needs its prefab/container refs), reached via Instance.
public class EnemyPreviewPanel : MonoBehaviour
{
    public static EnemyPreviewPanel Instance { get; private set; }

    [SerializeField] GameObject root;              // toggled on Show/Hide
    [SerializeField] RectTransform panelRect;      // moved to the screen position
    [SerializeField] Transform entryContainer;     // parent for spawned entries
    [SerializeField] EnemyPreviewEntry entryPrefab;
    [SerializeField] GameObject blindState;        // the "You cannot see..." object
    [SerializeField] TextMeshProUGUI blindText;

    void Awake()
    {
        Instance = this;
        if (root != null) root.SetActive(false);
    }

    public void Show(IReadOnlyList<EnemiesSO> enemies, Vector3 screenPosition)
    {
        Clear();

        var visible = new List<bool>(enemies.Count);
        for (int i = 0; i < enemies.Count; i++)
            visible.Add(PreviewRules.CanPreview()); // no blind source today → all visible

        if (PreviewRules.EncounterVisible(visible))
        {
            blindState.SetActive(false);
            for (int i = 0; i < enemies.Count; i++)
            {
                var entry = Instantiate(entryPrefab, entryContainer);
                entry.Populate(enemies[i]);
            }
        }
        else
        {
            blindState.SetActive(true);
            blindText.text = enemies.Count == 1
                ? "You cannot see the enemy you are about to confront."
                : "You cannot see the enemies you are about to confront.";
        }

        panelRect.position = screenPosition;
        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        Clear();
    }

    void Clear()
    {
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
            Destroy(entryContainer.GetChild(i).gameObject);
        if (blindState != null) blindState.SetActive(false);
    }
}
