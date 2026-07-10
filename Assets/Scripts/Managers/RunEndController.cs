using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ends the run: one modal canvas for every outcome. Outcomes are REQUESTED
// during a frame and applied once in LateUpdate, so a Victory and a loss
// landing on the same frame resolve in the player's favor (win-first rule,
// spec 2026-07-07). Scene-placed singleton (needs canvas refs in the inspector).
public class RunEndController : MonoBehaviour
{
    public static RunEndController Instance { get; private set; }
    public static bool HasEnded => Instance != null && Instance.ended;

    [SerializeField] Canvas runEndCanvas;
    [SerializeField] TextMeshProUGUI headlineText;
    [SerializeField] TextMeshProUGUI statsText;

    private RunOutcome pending = RunOutcome.None;
    private bool ended;

    void Awake()
    {
        Instance = this;
        runEndCanvas.gameObject.SetActive(true); // GameManager.Awake canvas idiom
        runEndCanvas.enabled = false;
    }

    public static void RequestEnd(RunOutcome outcome)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"RunEndController missing from scene; outcome '{outcome}' dropped.");
            return;
        }
        Instance.Queue(outcome);
    }

    private void Queue(RunOutcome outcome)
    {
        if (ended || outcome == RunOutcome.None) return;
        // First request wins the slot, except Victory always takes it.
        if (pending == RunOutcome.None || outcome == RunOutcome.Victory)
            pending = outcome;
    }

    void LateUpdate()
    {
        if (ended || pending == RunOutcome.None) return;
        ended = true;
        Show(pending);
        // A finished run can never be resumed.
        if (DataManager.Instance != null) DataManager.Instance.DeleteSave();
    }

    private void Show(RunOutcome outcome)
    {
        headlineText.text = outcome switch
        {
            RunOutcome.Victory  => "Victory! You have Risen to Archon!",
            RunOutcome.DoomLoss => "The Land Has Fallen",
            _                   => "Overcome by Wounds",
        };

        var player = FindAnyObjectByType<Player>();
        statsText.text =
            $"Rounds survived: {GameManager.Instance.Round}\n" +
            $"Castles conquered: {ConquestTracker.Instance.ConqueredCastleCount()}\n" +
            $"Level reached: {(player != null ? player.PlayerLevel : 0)}";

        // The run-end screen owns the display: shut every other canvas so nothing
        // (a lingering retreat message, an open combat/town panel) renders over it
        // or steals the pointer from the Main Menu button.
        var gm = GameManager.Instance;
        if (gm != null)
        {
            if (gm.messageCanvas != null)    gm.messageCanvas.enabled = false;
            if (gm.combatCanvas != null)     gm.combatCanvas.enabled = false;
            if (gm.townCanvas != null)       gm.townCanvas.enabled = false;
            if (gm.cardRewardCanvas != null) gm.cardRewardCanvas.enabled = false;
            if (gm.cardListCanvas != null)   gm.cardListCanvas.enabled = false;
            if (gm.cardCanvas != null)       gm.cardCanvas.enabled = false;
            if (gm.mainMenuCanvas != null)   gm.mainMenuCanvas.enabled = false;
        }

        runEndCanvas.enabled = true;
    }

    // Wired to the run-end canvas button in the editor.
    public void MainMenuButton() => SceneManager.LoadScene(0);
}
