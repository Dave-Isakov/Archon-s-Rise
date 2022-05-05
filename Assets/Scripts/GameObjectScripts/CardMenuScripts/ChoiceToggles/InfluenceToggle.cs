using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfluenceToggle : ChoiceToggles
{
    private void Awake() {
        _cardType = StatType.Influence;
    }

}
