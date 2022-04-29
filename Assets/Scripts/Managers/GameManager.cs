using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance { get { return instance; } }

    public Canvas messageCanvas;
    public GameObject enlargeCardPosition;
    public GameObject cardCanvas;
    public GameObject playerHand;
    public PlayManager commands;

    public Button returnButton;
    public TextMeshProUGUI messageText;

    private void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        cardCanvas.SetActive(true);
        messageCanvas.gameObject.SetActive(true);
    }

    private void Start() 
    {
        commands = new PlayManager();
    }

    public void ReturnButton()
    {
        messageCanvas.sortingOrder = -100;
    }

    public void ValidationMessage(string message)
    {
        messageCanvas.sortingOrder = 100;
        messageText.text = message;
    }
}
