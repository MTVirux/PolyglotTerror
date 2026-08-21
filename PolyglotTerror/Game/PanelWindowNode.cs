using System.Collections.Generic;
using System.Numerics;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

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

    private readonly NineGridNode background;
    private bool styled;

    // The base class insists on a node to hand focus to. There is no header here, so it gets an
    // empty one that never draws.
    private readonly ResNode focusStandIn = new() { IsVisible = false };

    public PanelWindowNode()
    {
        // Deliberately not SimpleNineGridNode: that wrapper manages a single part and re-applies it,
        // which undoes the nine copied from the tooltip the first time the panel is resized.
        background = new NineGridNode
        {
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

    /// <summary>
    /// Rebuilds the background from the tooltip's own nine grid.
    /// </summary>
    /// <remarks>
    /// A real nine grid is nine separate pieces of the atlas, and the render type tells the game to
    /// draw all of them. One part with that render type is what crashed the game - the renderer ran
    /// off the end of the list - so the parts go in first and the render type only after.
    /// </remarks>
    public void ApplyStyle(
        string texturePath,
        IReadOnlyList<Vector4> rects,
        uint partId,
        byte renderType,
        uint blendMode,
        short top,
        short right,
        short bottom,
        short left)
    {
        var parts = new List<Part>(rects.Count);
        foreach (var rect in rects)
        {
            parts.Add(new Part
            {
                TexturePath = texturePath,
                U = (ushort)rect.X,
                V = (ushort)rect.Y,
                Width = (ushort)rect.Z,
                Height = (ushort)rect.W,
            });
        }

        background.Parts = parts;
        background.PartId = partId;
        background.TopOffset = top;
        background.RightOffset = right;
        background.BottomOffset = bottom;
        background.LeftOffset = left;
        background.BlendMode = blendMode;
        background.PartsRenderType = renderType;
        styled = true;
    }

    public override void SetTitle(string title, string? subtitle)
    {
        // There is no header to put a title in.
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        background.Size = Size;
    }

    /// <summary>Whether the tooltip's own parts have been copied in yet.</summary>
    public bool Styled => styled;
}
