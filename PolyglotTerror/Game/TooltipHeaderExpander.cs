using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PolyglotTerror.Game;

/// <summary>
/// Makes room for extra name lines in a tooltip header.
/// </summary>
/// <remarks>
/// The game measures a tooltip's description block from its text but gives the name a fixed
/// two-line region, so extra lines written into the name render over the row beneath it. After the
/// game has laid the tooltip out we grow the name node by what the game says its text draws, grow
/// every ancestor by the same amount, and at each level push lower siblings down while growing the
/// ones that span the node, so frames and collision boxes keep up with the content.
/// </remarks>
public sealed unsafe class TooltipHeaderExpander
{
    private readonly Configuration config;
    private readonly Dictionary<nint, Applied> applied = new();

    public TooltipHeaderExpander(Configuration config) => this.config = config;

    public void Expand(AtkUnitBase* unit, string? appendedText, bool log = false)
    {
        if (unit == null || string.IsNullOrEmpty(appendedText))
            return;

        var node = FindNode(unit, appendedText);
        if (node == null)
            return;

        var lineSpacing = node->LineSpacing;
        if (lineSpacing == 0)
            return;

        var key = (nint)node;
        var height = node->AtkResNode.Height;
        var known = applied.TryGetValue(key, out var last) && last.Height == height;

        // The game rebuilds the layout on most updates, but not all, so only trust remembered
        // pristine values while the node still has the height we last gave it.
        var pristine = known ? last.Pristine : height;
        var pristineY = known ? last.Y : node->AtkResNode.Y;

        // Ask the game how tall the text actually draws - counting newlines misses word wrapping
        // and assumes the line advance equals LineSpacing. Keep the arithmetic as a floor in case
        // the measurement comes back unwrapped.
        ushort drawWidth = 0;
        ushort drawHeight = 0;
        node->GetTextDrawSize(&drawWidth, &drawHeight);

        var lines = CountLines(node->NodeText.ToString());
        var content = Math.Max(drawHeight, lines * lineSpacing);
        var padding = pristine % lineSpacing;
        var topOffset = config.TooltipNameTopOffset;
        var target = content + padding + topOffset + config.TooltipNameExtraSpace;
        var delta = target - height;

        if (log)
        {
            Plugin.Log.Information(
                $"Header expand: pristine={pristine} height={height} lineSpacing={lineSpacing} " +
                $"lines={lines} drawn={drawWidth}x{drawHeight} padding={padding} target={target} delta={delta}");
        }

        if (delta <= 0)
        {
            if (target <= pristine)
                applied.Remove(key);

            return;
        }

        GrowAncestors((AtkResNode*)node, delta);
        node->AtkResNode.SetYFloat(pristineY + topOffset);
        applied[key] = new Applied(pristine, (ushort)target, pristineY);
    }

    public void Clear() => applied.Clear();

    private static void GrowAncestors(AtkResNode* node, int delta)
    {
        var current = node;
        while (current != null)
        {
            var parent = current->ParentNode;
            var top = current->Y;
            var bottom = top + current->Height;
            current->SetHeight((ushort)(current->Height + delta));

            if (parent != null)
                AdjustSiblings(parent, current, top, bottom, delta);

            current = parent;
        }
    }

    /// <summary>
    /// A sibling that spans the grown node is a frame, background or collision box and has to grow
    /// with it; one that sits below it is content and has to move down.
    /// </summary>
    private static void AdjustSiblings(AtkResNode* parent, AtkResNode* grown, float top, float bottom, int delta)
    {
        for (var child = parent->ChildNode; child != null; child = child->PrevSiblingNode)
        {
            if (child == grown)
                continue;

            if (child->Y > top)
            {
                child->SetYFloat(child->Y + delta);
                continue;
            }

            if (child->Y + child->Height >= bottom)
                child->SetHeight((ushort)(child->Height + delta));
        }
    }

    private static AtkTextNode* FindNode(AtkUnitBase* unit, string appendedText)
    {
        var count = unit->UldManager.NodeListCount;
        for (var i = 0; i < count; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text)
                continue;

            var text = (AtkTextNode*)node;
            if (text->NodeText.ToString().EndsWith(appendedText, StringComparison.Ordinal))
                return text;
        }

        return null;
    }

    private static int CountLines(string text)
    {
        var lines = 1;
        foreach (var character in text)
        {
            if (character == '\n')
                lines++;
        }

        return lines;
    }

    private readonly record struct Applied(ushort Pristine, ushort Height, float Y);
}
