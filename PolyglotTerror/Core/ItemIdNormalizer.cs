namespace PolyglotTerror.Core;

public static class ItemIdNormalizer
{
    private const uint EventItemFloor = 2_000_000;
    private const uint HighQualityOffset = 1_000_000;
    private const uint CollectableOffset = 500_000;

    // Hovered item ids carry quality in the id itself. Event items are already
    // their own sheet's row ids, so they pass through untouched.
    public static uint ToBaseItemId(uint itemId) => itemId switch
    {
        >= EventItemFloor => itemId,
        >= HighQualityOffset => itemId - HighQualityOffset,
        >= CollectableOffset => itemId - CollectableOffset,
        _ => itemId,
    };

    public static bool IsEventItem(uint itemId) => itemId >= EventItemFloor;
}
