using UnityEngine;

// Inspector wrapper for the doom/spawn balance knobs (LevelRewardsSO pattern:
// one asset, every value tunable with no code change).
[CreateAssetMenu(fileName = "DoomTuning", menuName = "ScriptableObjects/DoomTuning")]
public class DoomTuningSO : ScriptableObject
{
    public DoomTuning tuning = new DoomTuning();
}
