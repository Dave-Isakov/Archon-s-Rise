using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefendToggle : ChoiceToggles
{
    private void Awake() {
        _cardType = CardType.Defend;
    }

}
