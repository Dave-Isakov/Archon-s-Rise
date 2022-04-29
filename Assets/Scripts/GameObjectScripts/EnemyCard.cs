using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class EnemyCard : MonoBehaviour, IPointerClickHandler
{   
    public EnemiesSO enemySO;
    [SerializeField] private TextMeshProUGUI enemyName;
    [SerializeField] private TextMeshProUGUI enemyHP;
    [SerializeField] private TextMeshProUGUI enemyAttack;
    [SerializeField] private EnemyCardEvent onDefeat_WoundPlayer;
    private bool isDefeated = false;
    private bool isMaximized;

    void Start() 
    {
        enemyName.text = enemySO.cardName;
        enemyAttack.text = "//\n" + enemySO.enemyAttack.ToString();
        enemyHP.text = "<)\n" + enemySO.enemyHP.ToString();
        Debug.Log($"{enemySO.name} ({this.gameObject.name}) has entered the battlefield.");
    }

    void Update()
    {

    }

    public void DefeatMonster()
    {
        if(DataManager.Instance.playerAttack >= enemySO.enemyHP)
        {
            DataManager.Instance.playerAttack -= enemySO.enemyHP;
            CheckWounds();
            DataManager.Instance.playerDefend -= enemySO.enemyAttack;
            if (DataManager.Instance.playerDefend < 0) DataManager.Instance.playerDefend = 0;
            //GetReward();
            //ChooseReward();
            //AssignReward();
            isDefeated = true;
            GameManager.Instance.commands.ClearStack();
        }
        else
        {
            GameManager.Instance.ValidationMessage($"You need {enemySO.enemyHP} Attack in order to defeat this monster.");
        }
    }
    private void CheckWounds()
    {
        if (DataManager.Instance.playerDefend < enemySO.enemyAttack)
        {
            int woundCount = 0;
            for (var i = 0; i < enemySO.enemyAttack; i += DataManager.Instance.playerHP)
            {
                onDefeat_WoundPlayer.Raise(this);
                woundCount++;
            }
            GameManager.Instance.ValidationMessage($"{enemySO.name} has been destroyed! You are wounded {woundCount} times!");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        DefeatMonster();
        if(isDefeated == true)
        {
            isDefeated = false;
            Destroy(this.gameObject, 1f * Time.deltaTime);
            GameManager.Instance.ValidationMessage($"{enemySO.name} has been destroyed!");
        }
    }
}
