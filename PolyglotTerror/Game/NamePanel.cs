using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
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

    private TextNode? text;
    private string pending = string.Empty;

    [SetsRequiredMembers]
    public NamePanel()
    {
        InternalName = "PolyglotNames";
        Title = string.Empty;
        DisableClose = true;
        RespectCloseAll = false;
        OpenWindowSoundEffectId = 0;
        Size = new Vector2(280f, 120f);
    }

    /// <summary>The size this panel wants for the lines it is holding.</summary>
    public static Vector2 SizeFor(string[] lines)
        => new(280f, Math.Max(1, lines.Length) * LineHeight + (Padding * 4f));

    public void SetLines(string[] lines)
    {
        pending = string.Join('\n', lines);

        if (text is not null)
            text.String = pending;
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
    {
        text = new TextNode
        {
            Position = ContentStartPosition,
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
    }
}
