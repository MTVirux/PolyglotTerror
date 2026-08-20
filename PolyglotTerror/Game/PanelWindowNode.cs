using System.Numerics;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace PolyglotTerror.Game;

/// <summary>
/// A window with no header - just a border around its content.
/// </summary>
/// <remarks>
/// KamiToolKit's stock window draws a title bar, close button and dividing line, which is a lot of
/// furniture for a panel that sits next to a tooltip and holds three lines of text.
/// </remarks>
public sealed class PanelWindowNode : WindowNodeBase
{
    private const float Inset = 8f;

    private readonly BorderNineGridNode border;

    // The base class insists on a node to hand focus to. There is no header here, so it gets an
    // empty one that never draws.
    private readonly ResNode focusStandIn = new() { IsVisible = false };

    public PanelWindowNode()
    {
        border = new BorderNineGridNode
        {
            Position = Vector2.Zero,
            Size = Size,
            IsVisible = true,
        };

        border.AttachNode(this, NodePosition.AsLastChild);
        focusStandIn.AttachNode(this, NodePosition.AsLastChild);
    }

    public override float HeaderHeight => 0f;

    public override Vector2 ContentSize => Size - new Vector2(Inset * 2f, Inset * 2f);

    public override Vector2 ContentStartPosition => new(Inset, Inset);

    public override ResNode WindowHeaderFocusNode => focusStandIn;

    public override void SetTitle(string title, string? subtitle)
    {
        // There is no header to put a title in.
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        border.Size = Size;
    }
}
