using UnityEngine;

// Always-available town-menu controller. Its job today: revive every town
// button when the menu opens, before the open/influence events are raised.
//
// Why this is needed: each button hides itself with SetActive(false) so the
// VerticalLayoutGroup collapses cleanly. But the GameEventListener that later
// re-shows it lives on the SAME GameObject, and GameEventListener unregisters
// in OnDisable. So once a button hides, it stops receiving the influence event
// that runs UpdateButtonText and can never re-show itself — e.g. a Keep's
// Recruit button after its guardians are beaten, leaving the reopened menu
// empty. Re-activating all buttons here re-registers those listeners (OnEnable)
// so each button re-evaluates its own visibility from PlaceRules + conquest.
//
// Lazily creates its own scene GameObject (mirrors ConquestTracker /
// GuardianAssault) so no scene wiring is required; being scene-scoped means a
// new run starts fresh.
public class TownMenu : MonoBehaviour
{
    private static TownMenu instance;
    public static TownMenu Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("TownMenu").AddComponent<TownMenu>();
            return instance;
        }
    }

    // Re-activate every town button (including ones that hid themselves) so
    // their listeners are registered when the open/influence events fire. Any
    // button that should stay hidden is re-hidden by its own UpdateButtonText
    // in the same frame, so there is no visible flicker.
    public void PrepareButtons()
    {
        foreach (var button in FindObjectsByType<TownButtons>(FindObjectsInactive.Include))
            button.gameObject.SetActive(true);
    }
}
