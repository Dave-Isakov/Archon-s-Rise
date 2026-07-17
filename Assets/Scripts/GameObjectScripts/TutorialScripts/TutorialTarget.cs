using System.Collections.Generic;
using UnityEngine;

// Dumb highlight anchor (M2.12): drop on any UI element (or world object) and
// give it a stable id. Registers on enable, unregisters on disable, so the
// tutorial never hard-references other canvases' internals and the frame
// simply hides while a target's panel is closed. Last-enabled wins on id
// collisions. Input-agnostic: a controller-focus pass can reuse the ids.
public class TutorialTarget : MonoBehaviour
{
    public string targetId;

    static readonly Dictionary<string, TutorialTarget> registry = new();

    void OnEnable() => Register();

    void OnDisable()
    {
        if (string.IsNullOrEmpty(targetId)) return;
        if (registry.TryGetValue(targetId, out var current) && current == this)
            registry.Remove(targetId);
    }

    void Register()
    {
        if (!string.IsNullOrEmpty(targetId)) registry[targetId] = this;
    }

    // Runtime tagging (the guaranteed starter enemy): AddComponent runs
    // OnEnable before the id is assigned, so Attach registers explicitly.
    public static TutorialTarget Attach(GameObject go, string id)
    {
        var t = go.AddComponent<TutorialTarget>();
        t.targetId = id;
        t.Register();
        return t;
    }

    public static TutorialTarget Find(string id)
    {
        registry.TryGetValue(id, out var t);
        return t;
    }
}
