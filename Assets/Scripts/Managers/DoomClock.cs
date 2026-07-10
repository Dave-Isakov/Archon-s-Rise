using UnityEngine;

// Owns the run's doom value. Ticks +1 from GameManager.RoundPlus; everything
// else (event pushes, M3 content) goes through the same Add(). Scene-placed
// singleton (needs its tuning + event refs).
public class DoomClock : MonoBehaviour
{
    public static DoomClock Instance { get; private set; }

    [SerializeField] DoomTuningSO tuning;
    [SerializeField] IntEvent onDoomChanged;

    public int Doom { get; private set; }
    public DoomTuning Tuning => tuning.tuning;

    void Awake() => Instance = this;

    void Start() => onDoomChanged.Raise(Doom); // seed the HUD meter

    public void Add(int amount)
    {
        Doom = DoomRules.Add(Doom, amount, Tuning);
        onDoomChanged.Raise(Doom);
        if (DoomRules.IsLoss(Doom, Tuning))
            RunEndController.RequestEnd(RunOutcome.DoomLoss);
    }

    // Load path: restore without the loss check — a saved run is alive by
    // construction (the run-end screen deletes the save).
    public void SetLoaded(int doom)
    {
        Doom = doom;
        onDoomChanged.Raise(Doom);
    }
}
