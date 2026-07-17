using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] Button saveButton;
    // M2.12 tutorial controls (spec: no settings screen yet, so both live
    // here; a future settings screen can absorb them unchanged).
    [SerializeField] Toggle tutorialToggle;
    [SerializeField] Button resetTutorialButton;
    private Scene currentScene;
    private Scene mainMenuScene;

    private void Start() {
        currentScene = SceneManager.GetActiveScene();
        mainMenuScene = SceneManager.GetSceneByBuildIndex(0);

        if (tutorialToggle != null)
        {
            tutorialToggle.SetIsOnWithoutNotify(TutorialPrefs.Enabled);
            tutorialToggle.onValueChanged.AddListener(OnTutorialToggle);
        }
        if (resetTutorialButton != null)
            resetTutorialButton.onClick.AddListener(OnResetTutorial);
    }

    private void Update() {
        bool inGame = currentScene != mainMenuScene;
        saveButton.interactable = inGame;
        // Reset restarts the rail, which only exists in the game scene.
        if (resetTutorialButton != null) resetTutorialButton.interactable = inGame;
    }

    void OnTutorialToggle(bool on)
    {
        if (TutorialManager.Instance != null) TutorialManager.Instance.SetTipsEnabled(on);
        else TutorialPrefs.Enabled = on; // main-menu scene: persist for the run
    }

    void OnResetTutorial()
    {
        if (TutorialManager.Instance == null) return;
        TutorialManager.Instance.ResetTutorial();
        if (tutorialToggle != null) tutorialToggle.SetIsOnWithoutNotify(TutorialPrefs.Enabled);
    }
}
