using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class EnemyCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{   
    public EnemiesSO enemySO;
    [SerializeField] private TextMeshProUGUI enemyName;
    [SerializeField] private TextMeshProUGUI enemyHP;
    [SerializeField] private TextMeshProUGUI enemyAttack;
    [SerializeField] private EnemyCardEvent onDefeat_WoundPlayer;
    [SerializeField] EnemyCardEvent onClick_ValidatePlayerAttackToEnemyHP;
    private bool isDefeated = false;
    private bool isMaximized;

    void Start() 
    {
        enemyName.text = enemySO.cardName;
        enemyAttack.text = "//\n" + enemySO.enemyAttack.ToString();
        enemyHP.text = "<)\n" + enemySO.enemyHP.ToString();
        Debug.Log($"{enemySO.name} ({this.gameObject.name}) has entered the battlefield.");
    }
    public void DefeatMonster()
    {
        onClick_ValidatePlayerAttackToEnemyHP.Raise(this);     
    //         //ChooseReward();
    //         //AssignReward();
    //  isDefeated = true;
    //         GameManager.Instance.commands.ClearStack();
    }

    public void CheckWounds(Player player)
    {
        if (player.PlayerDefend < enemySO.enemyAttack)
        {
            int woundCount = 0;
            for (var i = 0; i < enemySO.enemyAttack-player.PlayerDefend; i += player.PlayerHP)
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        this.transform.localScale = new Vector3(2,2,2);
        GetComponent<Canvas>().overrideSorting = true;
        GetComponent<Canvas>().sortingOrder = 50;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        this.transform.localScale = new Vector3(1,1,1);
        GetComponent<Canvas>().overrideSorting = false;
        GetComponent<Canvas>().sortingOrder = 0;
    }

    public void DestroyEnemyObject()
    {
        isDefeated = true;
        GameManager.Instance.commands.ClearStack();
    }
}
