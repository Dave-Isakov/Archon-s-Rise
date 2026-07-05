using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class TownButtons : MonoBehaviour
{
    [SerializeField] protected TownToken _town;
    [SerializeField] protected TownEvent townEvent;
    [SerializeField] protected IntEvent influenceCostEvent;
    [SerializeField] protected Button thisButton;
    [SerializeField] protected TextMeshProUGUI buttonText;
    [SerializeField] protected int currentPlayerInfluence;

    protected void Awake()
    {
        
    }
    public void SetTownCard(TownToken town)
    {
        this._town = town;
    }

    // Read-only access for the preview trigger sharing this GameObject.
    public TownToken Town => _town;

    public void SetCurrentInfluence(int influence)
    {
        currentPlayerInfluence = influence;
    }

    public abstract void UpdateButtonText();
}
