using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] Button saveButton;
    private Scene currentScene;
    private Scene mainMenuScene;

    private void Start() {
        currentScene = SceneManager.GetActiveScene();
        mainMenuScene = SceneManager.GetSceneByBuildIndex(0);
    }

    private void Update() {        
        if(currentScene == mainMenuScene)
        {
            saveButton.interactable = false;
        }
        else
        {
            saveButton.interactable = true;
        }
    }
}
