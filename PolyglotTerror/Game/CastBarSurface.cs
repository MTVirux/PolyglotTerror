namespace PolyglotTerror.Game;

public enum CastSource
{
    Self,
    Target,
    FocusTarget,
}

/// <summary>
/// One cast bar we decorate. A <paramref name="TextNodeId"/> of 0 means the node is discovered
/// by matching the action name instead of being hardcoded.
/// </summary>
public sealed record CastBarSurface(string AddonName, uint TextNodeId, CastSource Source);
