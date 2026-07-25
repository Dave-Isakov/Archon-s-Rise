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
}
