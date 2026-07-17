using System.Collections.Generic;
using UnityEngine;

// The tutorial's device-level persistence (M2.12): PlayerPrefs only, keys
// namespaced "tut." — the codebase's first PlayerPrefs use, collision-free by
// construction. Run saves are untouched (schema stays v6).
public static class TutorialPrefs
{
    const string EnabledKey = "tut.enabled";
    const string RailStepKey = "tut.railStep";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static int RailStep
    {
        get => PlayerPrefs.GetInt(RailStepKey, 0);
        set { PlayerPrefs.SetInt(RailStepKey, value); PlayerPrefs.Save(); }
    }

    public static bool OneShotSeen(string id) => PlayerPrefs.GetInt("tut.oneshot." + id, 0) == 1;
    public static void MarkOneShot(string id) { PlayerPrefs.SetInt("tut.oneshot." + id, 1); PlayerPrefs.Save(); }

    public static bool HelpSeen(string panelId) => PlayerPrefs.GetInt("tut.help." + panelId, 0) == 1;
    public static void MarkHelp(string panelId) { PlayerPrefs.SetInt("tut.help." + panelId, 1); PlayerPrefs.Save(); }

    // Reset tutorial (demos, shared machines): deletes exactly the known keys.
    public static void ResetAll(IEnumerable<string> oneShotIds, IEnumerable<string> helpPanelIds)
    {
        PlayerPrefs.DeleteKey(EnabledKey);
        PlayerPrefs.DeleteKey(RailStepKey);
        foreach (var id in oneShotIds) PlayerPrefs.DeleteKey("tut.oneshot." + id);
        foreach (var id in helpPanelIds) PlayerPrefs.DeleteKey("tut.help." + id);
        PlayerPrefs.Save();
    }
}
