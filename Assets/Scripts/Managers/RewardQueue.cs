using UnityEngine;

// Unified modal arbiter (spec 2026-07-13): every reward/message modal — card
// picks, skill picks, validation messages, dungeon bundles — enqueues here and
// opens only when the previous one resolves. Wraps the pure ModalQueueCore.
// Lazily creates its scene object (ConquestTracker pattern) so no scene edit
// is needed; scene-scoped, so a new run starts blank.
public class RewardQueue : MonoBehaviour
{
    private readonly ModalQueueCore core = new ModalQueueCore();

    private static RewardQueue instance;
    public static RewardQueue Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("RewardQueue").AddComponent<RewardQueue>();
            return instance;
        }
    }

    public bool Busy => core.Busy;

    public void Enqueue(System.Action<System.Action> job) => core.Enqueue(job);

    // Run end: pending modals must never open over the terminal screen.
    public void Flush() => core.Flush();
}
