using System;

// Services a place can offer once its services are open (conquered, or a
// guardian-less Town). Derived from PlaceType via PlaceRules so designers
// cannot author invalid combos; the legacy TownsSO.activity flags no longer
// drive availability (Crystal is offered at every Place — see PlaceRules).
[Flags]
public enum PlaceService
{
    None = 0,
    Recruit = 1,
    Heal = 2,
    Cards = 4,
    Crystal = 8,
}
