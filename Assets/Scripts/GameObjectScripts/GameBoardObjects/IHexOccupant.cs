using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Anything that sits on a hex cell and can describe itself to the tooltip
// (spec 2026-07-24, §1c). Tokens register with HexOccupantRegistry on Start so
// the tooltip and move-blocking discover new tile types with no HexInteractor
// edits. A future tile type integrates by implementing this + registering.
public interface IHexOccupant
{
    Vector3Int Cell { get; }
    HexDescriptor Describe();
    // True when standing-on this occupant is a place-entry (towns/dungeons), so
    // HexInteractor must not dispatch a raw Move onto it. A passive tile the
    // player simply parks on (crystal hotspot) returns false.
    bool BlocksMove { get; }
}
