using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The rail/one-shot instruction banner (M2.12). Dumb view: TutorialManager
// decides what shows; the buttons call back into the manager (wired in the
// editor). Not a modal — never enqueued on RewardQueue; the manager's
// CanvasGroup hides it while modals or pickers are open.
public class TutorialBanner : MonoBehaviour
{
    [SerializeField] GameObject root; // the visible panel
    [SerializeField] TextMeshProUGUI bodyText;
    [SerializeField] Button nextButton;
    [SerializeField] TextMeshProUGUI nextLabel;
    [SerializeField] Button skipButton;

    public void ShowStep(string text, bool informational)
    {
        root.SetActive(true);
        bodyText.text = text;
        nextButton.gameObject.SetActive(informational);
        if (informational) nextLabel.text = "Next";
        skipButton.gameObject.SetActive(true);
    }

    public void ShowTip(string text)
    {
        root.SetActive(true);
        bodyText.text = text;
        nextButton.gameObject.SetActive(true);
        nextLabel.text = "Got it";
        skipButton.gameObject.SetActive(false);
    }

    public void HideAll() => root.SetActive(false);
}
