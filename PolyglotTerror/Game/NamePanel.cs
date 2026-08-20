using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace PolyglotTerror.Game;

/// <summary>
/// A small window of our own that carries the translated names, shown beside a tooltip.
/// </summary>
/// <remarks>
/// It is a KamiToolKit addon rather than a node inside the game's tooltip, so nothing here writes
/// to the game's own layout. That is the whole point: making room inside a tooltip means moving the
/// rows the game placed, and it is where the plugin used to crash.
/// </remarks>
public sealed unsafe class NamePanel : NativeAddon
{
    private const float LineHeight = 20f;
    private const float Padding = 8f;

    private TextNode? tag;
    private TextNode? text;
    private string pendingTag = string.Empty;
    private string pending = string.Empty;

    [SetsRequiredMembers]
    public NamePanel()
    {
        InternalName = "PolyglotNames";
        Title = string.Empty;
        DisableClose = true;
        DisableCloseTransition = true;
        RespectCloseAll = false;
        OpenWindowSoundEffectId = 0;
        Size = new Vector2(280f, 120f);
        CreateWindowNode = () => new PanelWindowNode();

        // A default window opens behind the inventory and shop menus the tooltip sits over, which
        // leaves the panel unreadable exactly when it is wanted.
        DepthLayer = OverlayLayer.AboveUserInterface.DepthLayer;
    }

    /// <summary>The size this panel wants for what it is holding.</summary>
    public static Vector2 SizeFor(string? tag, string[] lines)
    {
        var rows = Math.Max(1, lines.Length) + (string.IsNullOrEmpty(tag) ? 0 : 1);
        return new Vector2(280f, (rows * LineHeight) + (Padding * 4f));
    }

    /// <summary>
    /// Sets what the panel shows. <paramref name="languageTag"/> is null when every language is
    /// listed at once and there is no single one to name.
    /// </summary>
    public void SetContent(string? languageTag, string[] lines)
    {
        pendingTag = languageTag ?? string.Empty;
        pending = string.Join('\n', lines);

        if (tag is not null)
        {
            tag.String = pendingTag;
            tag.IsVisible = pendingTag.Length > 0;
        }

        if (text is not null)
        {
            text.Position = TextStart(pendingTag.Length > 0);
            text.String = pending;
        }
    }

    private Vector2 TextStart(bool tagged)
        => ContentStartPosition + new Vector2(0f, tagged ? LineHeight : 0f);

    /// <summary>Re-applies the hit testing opt-out, which the window restores when it is resized.</summary>
    public void SuppressInput() => MakeNonInteractive(this);

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        tag = new TextNode
        {
            Position = ContentStartPosition,
            Size = new Vector2(ContentSize.X, LineHeight),
            FontSize = 11,
            AlignmentType = AlignmentType.TopLeft,
            TextColor = new Vector4(0.6f, 0.6f, 0.55f, 1f),
            TextOutlineColor = new Vector4(0f, 0f, 0f, 1f),
            TextFlags = TextFlags.Edge,
            String = pendingTag,
            IsVisible = pendingTag.Length > 0,
        };

        tag.RemoveNodeFlags(NodeFlags.RespondToMouse, NodeFlags.EmitsEvents, NodeFlags.HasCollision);
        AddNode(tag);

        text = new TextNode
        {
            Position = TextStart(pendingTag.Length > 0),
            Size = ContentSize,
            FontSize = 12,
            AlignmentType = AlignmentType.TopLeft,
            TextColor = new Vector4(1f, 1f, 1f, 1f),
            TextOutlineColor = new Vector4(0f, 0f, 0f, 1f),
            TextFlags = TextFlags.Edge | TextFlags.MultiLine | TextFlags.WordWrap,
            String = pending,
            IsVisible = true,
        };

        text.RemoveNodeFlags(NodeFlags.RespondToMouse, NodeFlags.EmitsEvents, NodeFlags.HasCollision);
        AddNode(text);

        MakeNonInteractive(addon);
    }

    /// <summary>
    /// Takes the panel out of the game's hit testing.
    /// </summary>
    /// <remarks>
    /// It sits right beside the cursor, and a window that answers the mouse takes the hover away
    /// from whatever is under it. Clearing the addon's own collision list is not enough - the window
    /// component carries collision nodes of its own that never appear in it - so the whole tree is
    /// walked. Nothing here is clickable, so none of it has any business being hit tested.
    /// </remarks>
    private static void MakeNonInteractive(AtkUnitBase* addon)
    {
        if (addon == null)
            return;

        Strip(addon->RootNode, 0);
        Strip((AtkResNode*)addon->WindowCollisionNode, 0);
        Strip((AtkResNode*)addon->WindowHeaderCollisionNode, 0);

        for (var i = 0; i < addon->CollisionNodeListCount; i++)
            Strip(addon->CollisionNodeList[i], 0);
    }

    private static void Strip(AtkResNode* node, int depth)
    {
        const NodeFlags interactive = NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;

        if (depth > 32)
            return;

        for (; node != null; node = node->PrevSiblingNode)
        {
            node->NodeFlags &= ~interactive;
            Strip(node->ChildNode, depth + 1);
        }
    }
}
