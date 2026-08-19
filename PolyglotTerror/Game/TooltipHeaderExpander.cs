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
        {
            if (log)
                Plugin.Log.Information($"Header expand: no appended text (unit={(nint)unit:X})");

            return;
        }

        var node = FindNode(unit, appendedText);
        if (node == null)
        {
            if (log)
                LogMissingNode(unit, appendedText);

            return;
        }

        var lineSpacing = node->LineSpacing;
        if (lineSpacing == 0)
        {
            if (log)
                Plugin.Log.Information("Header expand: node has no line spacing");

            return;
        }

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

        // The node's own newlines may be re-encoded, so fall back to the line count we wrote.
        var lines = Math.Max(CountLines(node->NodeText.ToString()), Lines(appendedText).Length + 1);
        var content = Math.Max(drawHeight, lines * lineSpacing);
        var padding = pristine % lineSpacing;
        var topOffset = config.TooltipNameTopOffset;
        var target = content + padding + topOffset + config.TooltipNameExtraSpace;
        var delta = target - height;

        if (log)
        {
            Plugin.Log.Information(
                $"Header expand: pristine={pristine} height={height} lineSpacing={lineSpacing} " +
                $"lines={lines} drawn={drawWidth}x{drawHeight} padding={padding} " +
                $"space={config.TooltipNameExtraSpace} topOffset={topOffset} known={known} " +
                $"target={target} delta={delta}");
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

        if (log)
        {
            var parent = node->AtkResNode.ParentNode;
            Plugin.Log.Information(
                $"Header expand applied: node={node->AtkResNode.Height}h y={node->AtkResNode.Y} " +
                $"parent={(parent == null ? 0 : parent->Height)}h");
        }
    }

    public void Clear() => applied.Clear();

    private static void GrowAncestors(AtkResNode* node, int delta)
    {
        var current = node;
        while (current != null)
        {
            var parent = current->ParentNode;
            var top = current->Y;
            current->SetHeight((ushort)(current->Height + delta));

            if (parent != null)
                AdjustSiblings(parent, current, top, delta);

            current = parent;
        }
    }

    /// <summary>
    /// A sibling below the grown node is content and moves down. A sibling covering the parent from
    /// top to bottom is a frame, background or collision box and grows instead. Anything else is left
    /// alone - the item icon sits beside the name and spans it, but stretching it would distort it.
    /// </summary>
    private static void AdjustSiblings(AtkResNode* parent, AtkResNode* grown, float top, int delta)
    {
        var parentHeight = parent->Height;

        for (var child = parent->ChildNode; child != null; child = child->PrevSiblingNode)
        {
            if (child == grown)
                continue;

            if (child->Y > top)
            {
                child->SetYFloat(child->Y + delta);
                continue;
            }

            if (child->Y <= 0 && child->Y + child->Height >= parentHeight)
                child->SetHeight((ushort)(child->Height + delta));
        }
    }

    /// <summary>
    /// Reports why the tail did not match any node, since that is invisible from the node dump.
    /// </summary>
    private static void LogMissingNode(AtkUnitBase* unit, string appendedText)
    {
        Plugin.Log.Information(
            $"Header expand: no node contains the appended tail ({appendedText.Length} chars): \"{appendedText}\"");

        var count = unit->UldManager.NodeListCount;
        for (var i = 0; i < count; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text)
                continue;

            var text = ((AtkTextNode*)node)->NodeText.ToString();
            if (text.Contains('\n'))
                Plugin.Log.Information($"  candidate id={node->NodeId} ({text.Length} chars): \"{text}\"");
        }
    }

    private static AtkTextNode* FindNode(AtkUnitBase* unit, string appendedText)
    {
        var wanted = Lines(appendedText);
        if (wanted.Length == 0)
            return null;

        var count = unit->UldManager.NodeListCount;
        for (var i = 0; i < count; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text)
                continue;

            var text = ((AtkTextNode*)node)->NodeText.ToString();
            if (ContainsAll(text, wanted))
                return (AtkTextNode*)node;
        }

        return null;
    }

    /// <summary>
    /// Matches on whole lines rather than on the newline-joined tail. The game re-encodes the string
    /// when it copies it into the node, so the newline bytes there are not the ones we wrote and any
    /// comparison spanning a line break fails even though the text is identical.
    /// </summary>
    private static bool ContainsAll(string text, string[] wanted)
    {
        foreach (var line in wanted)
        {
            if (!text.Contains(line, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string[] Lines(string text)
    {
        var lines = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                lines.Add(trimmed);
        }

        return lines.ToArray();
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
