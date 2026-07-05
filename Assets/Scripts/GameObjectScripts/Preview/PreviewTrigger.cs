using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Input adapter for the enemy preview. Decouples the preview request from the
// input event: mouse hover calls Focus/Unfocus today; a gamepad ISelect/IDeselect
// will call the SAME two methods at the controller milestone, with no change to
// the panel or the rules. Subclasses resolve which enemies this source previews
// and where to anchor the panel on screen.
public abstract class PreviewTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    protected abstract IReadOnlyList<EnemiesSO> ResolveEnemies();
    protected abstract Vector3 ScreenPosition();

    public void Focus()
    {
        if (EnemyPreviewPanel.Instance == null) return;
        var enemies = ResolveEnemies();
        if (enemies == null || enemies.Count == 0) return;
        EnemyPreviewPanel.Instance.Show(enemies, ScreenPosition());
    }

    public void Unfocus()
    {
        if (EnemyPreviewPanel.Instance != null)
            EnemyPreviewPanel.Instance.Hide();
    }

    public void OnPointerEnter(PointerEventData eventData) => Focus();
    public void OnPointerExit(PointerEventData eventData) => Unfocus();
}
