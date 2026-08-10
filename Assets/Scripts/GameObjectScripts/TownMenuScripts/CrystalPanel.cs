using UnityEngine;

// The crystal pop-out, on the RecruitPanel visibility convention: its own root
// Canvas, GameObject always active, Start() force-closes it.
//
// It used to live inside TownMenuCanvas. That canvas is disabled on the place-fan
// route, so the fan raised the open event and rendered nothing — the pop-out was
// only ever reachable from the town menu.
[RequireComponent(typeof(Canvas))]
public class CrystalPanel : MonoBehaviour
{
    Canvas _canvas;
    Canvas Canvas => _canvas != null ? _canvas : (_canvas = GetComponent<Canvas>());

    static CrystalPanel instance;
    public static CrystalPanel Instance
        => instance != null
            ? instance
            : (instance = FindAnyObjectByType<CrystalPanel>(FindObjectsInactive.Include));

    void Start()
    {
        Canvas.enabled = false;
    }

    // Wired to OnCrystalButtonClick_CreateCrystalButtons, which both the town menu's
    // CrystalButton and TownToken's fan route raise.
    public void Open()
    {
        Canvas.enabled = true;
    }

    public void Close()
    {
        Canvas.enabled = false;
    }
}
