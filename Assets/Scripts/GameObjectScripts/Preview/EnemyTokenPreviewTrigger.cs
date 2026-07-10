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

    protected override IReadOnlyList<EnemyPreviewData> ResolveEntries()
        => token.enemy != null && !MapFog.IsHidden(token.gridPos)   // fogged tokens are not previewable
            ? new List<EnemyPreviewData> { new EnemyPreviewData(token.enemy, token.bonusAttack, token.bonusHP) }
            : new List<EnemyPreviewData>();

    protected override Vector3 ScreenPosition()
    {
        var cam = Camera.main;
        return cam != null ? cam.WorldToScreenPoint(transform.position) : transform.position;
    }
}
