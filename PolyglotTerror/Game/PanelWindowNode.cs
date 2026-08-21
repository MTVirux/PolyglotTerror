using System.Numerics;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;

namespace PolyglotTerror.Game;

/// <summary>
/// The background an item tooltip draws, with nothing else on it.
/// </summary>
/// <remarks>
/// Taken from ItemDetail itself: its whole frame is one nine grid, so the numbers here are the ones
/// the game uses rather than an approximation of them. The stock window would look close enough,
/// but it builds a title, subtitle, two backgrounds, three buttons and their collision every time
/// it opens - around twenty nodes per panel, none of which this needs.
/// </remarks>
public sealed class PanelWindowNode : WindowNodeBase
{
    private const float Inset = 16f;

    private readonly SimpleNineGridNode background;

    // The base class insists on a node to hand focus to. There is no header here, so it gets an
    // empty one that never draws.
    private readonly ResNode focusStandIn = new() { IsVisible = false };

    public PanelWindowNode()
    {
        background = new SimpleNineGridNode
        {
            TexturePath = "ui/uld/img06/WindowF_BgNormal_Corner.tex",
            TextureCoordinates = Vector2.Zero,
            TextureSize = new Vector2(16f, 64f),
            TopOffset = 64,
            RightOffset = 32,
            BottomOffset = 32,
            LeftOffset = 32,
            Position = Vector2.Zero,
            Size = Size,
            IsVisible = true,
        };

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
