using UnityEngine;

// The seam between input devices and the board interactor. The mouse implementation
// ships now; a controller-cursor implementation drops in later without touching
// HexInteractor. TryGetCell returns false when the pointer is over nothing usable
// (e.g. over UI, or off-screen).
public interface IHexPointerSource
{
    bool TryGetCell(out Vector3Int cell);
    bool ConfirmPressed { get; }
}
