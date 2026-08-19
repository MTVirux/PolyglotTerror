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
/// game has laid the tooltip out we grow the name node, grow every ancestor by the same amount, and
/// push each ancestor's lower siblings down so the blocks below the header follow.
/// </remarks>
public sealed unsafe class TooltipHeaderExpander
{
    private readonly Dictionary<nint, Applied> applied = new();

    public void Expand(AtkUnitBase* unit, string? appendedText)
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

        // The game rebuilds the layout on most updates, but not all, so only trust a remembered
        // pristine height while the node still has the height we last gave it.
        var pristine = applied.TryGetValue(key, out var last) && last.Height == height
            ? last.Pristine
            : height;

        var lines = CountLines(node->NodeText.ToString());
        var allowed = Math.Max(1, pristine / lineSpacing);
        if (lines <= allowed)
        {
            applied.Remove(key);
            return;
        }

        var padding = pristine - (allowed * lineSpacing);
        var target = (lines * lineSpacing) + padding;
        var delta = target - height;
        if (delta <= 0)
            return;

        GrowAncestors((AtkResNode*)node, delta);
        applied[key] = new Applied(pristine, (ushort)target);
    }

    public void Clear() => applied.Clear();

    private static void GrowAncestors(AtkResNode* node, int delta)
    {
        var current = node;
        while (current != null)
        {
            var parent = current->ParentNode;
            current->SetHeight((ushort)(current->Height + delta));

            if (parent != null)
                PushSiblingsDown(parent, current, delta);

            current = parent;
        }
    }

    private static void PushSiblingsDown(AtkResNode* parent, AtkResNode* grown, int delta)
    {
        for (var child = parent->ChildNode; child != null; child = child->PrevSiblingNode)
        {
            if (child != grown && child->Y > grown->Y)
                child->SetYFloat(child->Y + delta);
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

    private readonly record struct Applied(ushort Pristine, ushort Height);
}
