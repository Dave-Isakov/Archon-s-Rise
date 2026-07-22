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

    static string StepBannerById(string id)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TutorialStepSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<TutorialStepSO>(path);
            if (so != null && so.id == id) return so.bannerText ?? "";
        }
        return null;
    }

    // Pins the turn-phase rail copy (spec 2026-07-21): the Explore/Action/End
    // rhythm and the shrinking "day". RED until the rail assets are re-authored
    // (Task 10); GREEN once the phase copy is entered.
    [Test]
    public void PhaseRailStepsTeachExploreActionEndAndTheDay()
    {
        var explore = StepBannerById("move");
        Assert.IsNotNull(explore, "missing rail step id 'move'");
        Assert.That(explore, Does.Contain("Explore phase"));
        Assert.That(explore, Does.Contain("move"));

        var action = StepBannerById("fight");
        Assert.IsNotNull(action, "missing rail step id 'fight'");
        Assert.That(action, Does.Contain("one action"));

        var end = StepBannerById("end-turn");
        Assert.IsNotNull(end, "missing rail step id 'end-turn'");
        Assert.That(end, Does.Contain("End the turn"));
        Assert.That(end, Does.Contain("day"));
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
