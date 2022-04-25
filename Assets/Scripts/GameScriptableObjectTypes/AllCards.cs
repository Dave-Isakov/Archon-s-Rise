using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AllCards : ScriptableObject
{
    public string cardName;
    [TextArea(2,4)] public string cardDescription;
}
