using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttackToggle : ChoiceToggles
{
    private void Awake() {
        _cardType = StatType.Attack;
    }
}
