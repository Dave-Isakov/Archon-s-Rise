using UnityEngine;
using ArchonsRise.HexTooltipInfo;

// Map-side crystal-hotspot identity (spec 2026-07-24). Registers charges with
// HotspotTracker + itself with HexOccupantRegistry on Start. Passive: NO click
// handler — harvesting fires from the turn-end chain, not interaction. Visual
// states: live (colored crystal + charge pip) vs dormant (greyed, depleted).
public class CrystalHotspotToken : MonoBehaviour, IHexOccupant
{
    public CrystalHotspotSO hotspotSO;
    public Vector3Int gridPos;                 // assigned by GridGeneration at spawn
    [SerializeField] GameObject livePips;       // optional charge-count display
    [SerializeField] GameObject dormantMarker;  // active once depleted
    [SerializeField] SpriteRenderer crystalSprite; // tinted to hotspotSO.color

    public Vector3Int Cell => gridPos;
    // Passive: the player parks ON this tile to harvest, so it must never block
    // HexInteractor's move-dispatch (unlike place-like towns/dungeons).
    public bool BlocksMove => false;

    void Start()
    {
        HotspotTracker.Instance.Register(gridPos, hotspotSO.id, hotspotSO.charges, hotspotSO.color);
        HexOccupantRegistry.Instance.Register(this);
        RefreshVisual();
    }

    void OnDestroy()
    {
        if (HexOccupantRegistry.Instance != null) HexOccupantRegistry.Instance.Unregister(this);
    }

    public HexDescriptor Describe()
        => new HexDescriptor(
            TileDescriptor.Hotspot(hotspotSO.color, HotspotTracker.Instance.Remaining(gridPos)),
            TileDescriptor.TilePriority);

    public void RefreshVisual()
    {
        bool live = HotspotTracker.Instance.CanHarvest(gridPos);
        if (dormantMarker != null) dormantMarker.SetActive(!live);
        if (livePips != null) livePips.SetActive(live);
        // Show this color's crystal art (each CrystalHotspotSO carries its own).
        if (crystalSprite != null && hotspotSO != null && hotspotSO.crystalSprite != null)
            crystalSprite.sprite = hotspotSO.crystalSprite;
    }
}
