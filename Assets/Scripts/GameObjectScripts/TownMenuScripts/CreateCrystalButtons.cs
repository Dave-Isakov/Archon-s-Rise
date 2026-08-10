using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CreateCrystalButtons : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] EmpowerType color;
    [SerializeField] Button thisButton;
    [SerializeField] EmpowerColorEvent onCrystalButtonClick_CreateCrystalOfColor;

    public void OnPointerClick(PointerEventData eventData)
    {
        HideAll();
    }

    // Closes the pop-out after a purchase. Click-off is the ClickOffCatcher's job,
    // wired straight to CrystalPanel.Close — the panel owns a Canvas, so it needs
    // no arming and the buttons need no hiding of their own.
    public static void HideAll()
    {
        if (CrystalPanel.Instance != null) CrystalPanel.Instance.Close();
    }

    private void Start()
    {
        thisButton.onClick.RemoveAllListeners();
        thisButton.onClick.AddListener(() => onCrystalButtonClick_CreateCrystalOfColor.Raise(color));
    }
}
