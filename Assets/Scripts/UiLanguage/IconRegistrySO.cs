using System.Collections.Generic;
using UnityEngine;

// concept -> Sprite for Image-based UI (HUD, M2.12 canvas art). TMP tag names
// live in IconMarkup; the validation tests tie the two halves together.
// The one asset lives at Assets/Resources/IconRegistry.asset.
[CreateAssetMenu(fileName = "IconRegistry", menuName = "ArchonsRise/Icon Registry")]
public class IconRegistrySO : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public IconConcept concept;
        public Sprite sprite;
    }

    [Tooltip("Garish stand-in returned for any missing concept so a gap is loud.")]
    public Sprite placeholderSprite;
    public List<Entry> entries = new List<Entry>();

    static IconRegistrySO _instance;
    public static IconRegistrySO Instance
        => _instance != null ? _instance : (_instance = Resources.Load<IconRegistrySO>("IconRegistry"));

    public Sprite SpriteFor(IconConcept concept)
    {
        foreach (var e in entries)
            if (e.concept == concept && e.sprite != null) return e.sprite;
        Debug.LogError($"IconRegistry: no sprite for {concept} — showing placeholder.");
        return placeholderSprite;
    }
}
