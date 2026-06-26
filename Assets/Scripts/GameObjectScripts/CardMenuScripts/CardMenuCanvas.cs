using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardMenuCanvas : MonoBehaviour
{
    [SerializeField] Canvas menuCanvas;
    int startingSort = -100;
    int maxSort = 501;
    void Awake()
    {
        menuCanvas.sortingOrder = startingSort;
    }

    void Update()
    {
        
    }

    public void OnCanvas()
    {
        menuCanvas.sortingOrder = maxSort;
    }

    public void OffCanvas()
    {
        menuCanvas.sortingOrder = startingSort;
    }
}
