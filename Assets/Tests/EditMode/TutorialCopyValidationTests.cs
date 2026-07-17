using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Extends the M2.11 icon validation to M2.12 assets: every sprite tag in
// authored tutorial/help copy must resolve to a known IconMarkup name, and
// step/one-shot ids must be unique and non-empty. Passes vacuously until the
// user authors the assets (plan Task 8); it pins them from then on.
public class TutorialCopyValidationTests
{
    static readonly Regex TagRx = new Regex("<sprite=\"(?<n>[^\"]+)\"");

    static HashSet<string> KnownNames()
    {
        var known = new HashSet<string>();
        foreach (IconConcept c in System.Enum.GetValues(typeof(IconConcept)))
            known.Add(IconMarkup.TmpName(c));
        return known;
    }

    static IEnumerable<(string path, string text)> AuthoredCopy()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TutorialStepSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<TutorialStepSO>(path);
            if (so != null) yield return (path, so.bannerText);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:TutorialOneShotSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<TutorialOneShotSO>(path);
            if (so != null) yield return (path, so.bannerText);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:HelpEntrySO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<HelpEntrySO>(path);
            if (so != null) yield return (path, so.title + "\n" + so.body);
        }
    }

    [Test]
    public void TutorialCopyUsesKnownSpriteTags()
    {
        var known = KnownNames();
        foreach (var (path, text) in AuthoredCopy())
        {
            if (string.IsNullOrEmpty(text)) continue;
            foreach (Match m in TagRx.Matches(text))
                Assert.IsTrue(known.Contains(m.Groups["n"].Value),
                    $"{path}: unknown sprite tag '{m.Groups["n"].Value}'");
        }
    }

    [Test]
    public void StepAndOneShotIdsAreUniqueAndNonEmpty()
    {
        var ids = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:TutorialStepSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<TutorialStepSO>(path);
            Assert.IsFalse(string.IsNullOrEmpty(so.id), path + ": empty id");
            Assert.IsTrue(ids.Add("step." + so.id), path + ": duplicate id " + so.id);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:TutorialOneShotSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<TutorialOneShotSO>(path);
            Assert.IsFalse(string.IsNullOrEmpty(so.id), path + ": empty id");
            Assert.IsTrue(ids.Add("tip." + so.id), path + ": duplicate id " + so.id);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:HelpEntrySO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<HelpEntrySO>(path);
            Assert.IsFalse(string.IsNullOrEmpty(so.panelId), path + ": empty panelId");
            Assert.IsTrue(ids.Add("help." + so.panelId), path + ": duplicate panelId " + so.panelId);
        }
    }
}
