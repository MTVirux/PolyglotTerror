using System.Numerics;
using KamiToolKit.Nodes;

namespace PolyglotTerror.Game;

/// <summary>
/// The game's window, with the header furniture taken off.
/// </summary>
/// <remarks>
/// Building the frame from scratch means picking textures and losing the background that makes the
/// text readable. Keeping the game's window and hiding its title bar, buttons and dividing line
/// leaves the same background and border a tooltip has.
/// </remarks>
public sealed class PanelWindowNode : WindowNode
{
    private const float Inset = 10f;

    public PanelWindowNode()
    {
        ShowCloseButton = false;
        ShowConfigButton = false;
        ShowHelpButton = false;

        HeaderContainerNode.IsVisible = false;
        DividingLineNode.IsVisible = false;
        TitleNode.IsVisible = false;
        SubtitleNode.IsVisible = false;

        // Dragging the panel by a header that is not drawn would be a surprise, and its collision
        // is part of what steals the cursor from the item underneath.
        HeaderCollisionNode.IsVisible = false;
    }

    public override float HeaderHeight => 0f;

    public override Vector2 ContentStartPosition => new(Inset, Inset);

    public override Vector2 ContentSize => Size - new Vector2(Inset * 2f, Inset * 2f);
}
