using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

// Ties the icon language together: the registry asset is complete, every
// IconMarkup name resolves to a real TMP sprite asset, and authored SO
// descriptions only use known tags in canonical per-line stat order.
// RED until the user authors the assets (plan Task 3) — expected.
public class IconRegistryValidationTests
{
    static IEnumerable<IconConcept> Concepts()
    {
        foreach (IconConcept c in System.Enum.GetValues(typeof(IconConcept))) yield return c;
    }

    [Test]
    public void RegistryAssetIsComplete()
    {
        var reg = Resources.Load<IconRegistrySO>("IconRegistry");
        Assert.IsNotNull(reg, "Assets/Resources/IconRegistry.asset missing");
        Assert.IsNotNull(reg.placeholderSprite, "placeholderSprite unassigned");
        foreach (var c in Concepts())
            Assert.IsTrue(reg.entries.Exists(e => e.concept == c && e.sprite != null),
                $"registry has no sprite for {c}");
    }

    [Test]
    public void EveryConceptTmpAssetResolves()
    {
        foreach (var c in Concepts())
        {
            string name = IconMarkup.TmpName(c);
            Assert.IsFalse(string.IsNullOrEmpty(name), c + " has no tmp name");
            var asset = Resources.Load<TMP_SpriteAsset>("Sprite Assets/" + name);
            Assert.IsNotNull(asset, $"TMP sprite asset 'Sprite Assets/{name}' missing (concept {c})");
        }
    }

    static readonly Regex TagRx = new Regex("<sprite=\"(?<n>[^\"]+)\"");

    static HashSet<string> KnownNames()
    {
        var known = new HashSet<string>();
        foreach (var c in Concepts()) known.Add(IconMarkup.TmpName(c));
        return known;
    }

    static IEnumerable<(string path, string text)> AuthoredDescriptions()
    {
        foreach (string guid in AssetDatabase.FindAssets(
            "t:ScriptableObject", new[] { "Assets/Scripts/ScriptableObjectData" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (obj == null) continue;
            var prop = new SerializedObject(obj).FindProperty("cardDescription");
            if (prop != null && !string.IsNullOrEmpty(prop.stringValue))
                yield return (path, prop.stringValue);
        }
    }

    [Test]
    public void AuthoredDescriptionsUseKnownTags()
    {
        var known = KnownNames();
        foreach (var (path, text) in AuthoredDescriptions())
            foreach (Match m in TagRx.Matches(text))
                Assert.IsTrue(known.Contains(m.Groups["n"].Value),
                    $"{path}: unknown sprite tag '{m.Groups["n"].Value}'");
    }

    // Per line, action-stat icons must follow Attack, Defend, Explore, Influence.
    // Lines with a conversion arrow ("->" or "→") are directional, not lists.
    [Test]
    public void AuthoredDescriptionsListActionStatsInCanonicalOrder()
    {
        var rank = new Dictionary<string, int>
        {
            { IconMarkup.TmpName(IconConcept.Attack), 0 },
            { IconMarkup.TmpName(IconConcept.Defend), 1 },
            { IconMarkup.TmpName(IconConcept.Explore), 2 },
            { IconMarkup.TmpName(IconConcept.Influence), 3 },
        };
        foreach (var (path, text) in AuthoredDescriptions())
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Contains("->") || line.Contains("→")) continue;
                int last = -1;
                foreach (Match m in TagRx.Matches(line))
                {
                    int r;
                    if (!rank.TryGetValue(m.Groups["n"].Value, out r)) continue;
                    Assert.IsTrue(r >= last,
                        $"{path}: action stats out of canonical order in line '{line.Trim()}'");
                    last = r;
                }
            }
        }
    }
}
