using System.Collections.Generic;
using UnityEngine;

// Map-token preview source: previews the single enemy this token represents,
// anchored to the token's on-screen position. The token already receives pointer
// events (EnemyToken handles clicks), so hover works with no extra raycaster setup.
[RequireComponent(typeof(EnemyToken))]
public class EnemyTokenPreviewTrigger : PreviewTrigger
{
    EnemyToken token;

    void Awake() => token = GetComponent<EnemyToken>();

    protected override IReadOnlyList<EnemiesSO> ResolveEnemies()
        => token.enemy != null
            ? new List<EnemiesSO> { token.enemy }
            : new List<EnemiesSO>();

    protected override Vector3 ScreenPosition()
    {
        var cam = Camera.main;
        return cam != null ? cam.WorldToScreenPoint(transform.position) : transform.position;
    }
}
