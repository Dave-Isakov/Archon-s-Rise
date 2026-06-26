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
    public Canvas mainMenuCanvas;
    public GameObject enlargeCardPosition;
    public GameObject enlargeTownCardPosition;
    public Canvas cardCanvas;
    public Canvas combatCanvas;
    public GameObject enemyCardCombatPosition;
    public Canvas cardRewardCanvas;
    public Canvas cardListCanvas;
    public GameObject cardListParent;
    public Canvas townCanvas;
    public GameObject playerHand;
    public PlayManager commands;
    private int roundNum;
    private int turnNum;
    public Button returnButton;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI roundTurnText;

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

        cardCanvas.gameObject.SetActive(true);
        cardCanvas.enabled = false;
        cardListCanvas.gameObject.SetActive(true);
        cardListCanvas.enabled = false;
        cardRewardCanvas.gameObject.SetActive(true);
        cardRewardCanvas.enabled = false;
        messageCanvas.gameObject.SetActive(true);
        messageCanvas.enabled = false;
        mainMenuCanvas.gameObject.SetActive(true);
        mainMenuCanvas.enabled = false;
        townCanvas.gameObject.SetActive(true);
        townCanvas.enabled = false;
        combatCanvas.gameObject.SetActive(true);
        combatCanvas.enabled = false;
        roundNum = 1;
        turnNum = 1;
    }

    private void Start() 
    {
        commands = new PlayManager();
    }

    private void Update() {
        roundTurnText.text = "Round: " + roundNum + " Turn: " + turnNum;
    }

    public void ReturnButton()
    {
        messageCanvas.enabled = false;
    }

    public void ValidationMessage(string message)
    {
        messageCanvas.enabled = true;
        messageText.text = message;
    }

    public void TurnPlus()
    {
        turnNum++;
    }

    public void RoundPlus()
    {
        roundNum++;
    }

    public void CombatCanvasActive()
    {
        combatCanvas.enabled = true;
        combatCanvas.GetComponentInChildren<Animator>().enabled = true;
    }

    public void CheckCombatants()
    {
        if(enemyCardCombatPosition.transform.childCount == 1)
        {
            combatCanvas.enabled = false;
            combatCanvas.GetComponentInChildren<Animator>().enabled = false;
        }
    }


}
