using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EndRoundButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Button endRoundButton;
    [SerializeField] VoidEvent endTheRound;

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance.commands.ClearStack();
    }

    private void Start() 
    {
        endRoundButton.onClick.RemoveAllListeners();
        endRoundButton.onClick.AddListener(() => endTheRound.Raise());
    }   
}
