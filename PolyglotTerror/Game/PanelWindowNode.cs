using System.Numerics;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace PolyglotTerror.Game;

/// <summary>
/// A window frame with no header - the background a game window draws, and nothing else.
/// </summary>
/// <remarks>
/// The background is KamiToolKit's own, which sets up the nine parts a window frame needs. Building
/// one by hand from the tooltip's node did not work: its parts list is nine overlapping regions of a
/// corner texture, and reproducing that faithfully enough to render was three failed attempts.
/// </remarks>
public sealed class PanelWindowNode : WindowNodeBase
{
    private const float Inset = 16f;

    private readonly WindowBackgroundTextureNode background = new(false)
    {
        Position = Vector2.Zero,
        IsVisible = true,
    };

    // The base class insists on a node to hand focus to. There is no header here, so it gets an
    // empty one that never draws.
    private readonly ResNode focusStandIn = new() { IsVisible = false };

    public PanelWindowNode()
    {
        background.Size = Size;
        background.AttachNode(this, NodePosition.AsLastChild);
        focusStandIn.AttachNode(this, NodePosition.AsLastChild);
    }

    public override float HeaderHeight => 0f;

    public override Vector2 ContentStartPosition => new(Inset, Inset);

    public override Vector2 ContentSize => Size - new Vector2(Inset * 2f, Inset * 2f);

    public override ResNode WindowHeaderFocusNode => focusStandIn;

    public override void SetTitle(string title, string? subtitle)
    {
        // There is no header to put a title in.
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        background.Size = Size;
    }
}
