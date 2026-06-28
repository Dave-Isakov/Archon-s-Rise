#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Bakes the complete set of cards/units into the DataManager prefab's allCards /
// allUnits so the save/load content registry is complete in EVERY scene (the
// registry is built by whichever DataManager Awakes first, including when you
// press Play directly in the GameBoard scene). Run after adding/removing card or
// unit assets: Tools > Archon's Rise > Rebuild Content Registry.
public static class ContentRegistryPopulator
{
    private const string DataManagerPrefabPath = "Assets/Prefabs/Managers/DataManager.prefab";

    [MenuItem("Tools/Archon's Rise/Rebuild Content Registry")]
    public static void Rebuild()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DataManagerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Rebuild Content Registry: prefab not found at {DataManagerPrefabPath}");
            return;
        }

        var dm = prefab.GetComponent<DataManager>();
        if (dm == null)
        {
            Debug.LogError($"Rebuild Content Registry: no DataManager component on {DataManagerPrefabPath}");
            return;
        }

        var cards = LoadAll<CardsSO>().OrderBy(c => c.id).ToArray();
        var units = LoadAll<UnitsSO>().OrderBy(u => u.id).ToArray();

        // Duplicate ids would make ContentRegistry throw at runtime; surface them now.
        WarnDuplicateIds(cards.Select(c => c.id), "card");
        WarnDuplicateIds(units.Select(u => u.id), "unit");

        var so = new SerializedObject(dm);
        AssignArray(so, "allCards", cards);
        AssignArray(so, "allUnits", units);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log($"Rebuild Content Registry: baked {cards.Length} cards and {units.Length} units into {DataManagerPrefabPath}.");
    }

    private static T[] LoadAll<T>() where T : Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(a => a != null)
            .ToArray();
    }

    private static void AssignArray<T>(SerializedObject so, string propName, T[] values) where T : Object
    {
        var prop = so.FindProperty(propName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void WarnDuplicateIds(IEnumerable<string> ids, string label)
    {
        foreach (var dup in ids.Where(s => !string.IsNullOrEmpty(s))
                                .GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key))
            Debug.LogWarning($"Rebuild Content Registry: duplicate {label} id '{dup}' — the registry rejects duplicates at runtime.");
    }
}
#endif
