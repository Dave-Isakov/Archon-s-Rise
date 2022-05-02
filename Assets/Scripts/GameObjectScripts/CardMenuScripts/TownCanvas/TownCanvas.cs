using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TownCanvas : MonoBehaviour
{
    [SerializeField] Canvas menuCanvas;
    int startingSort = -100;
    int maxSort = 100;

    void Awake()
    {
        menuCanvas.sortingOrder = startingSort;
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

