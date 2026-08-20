using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PolyglotTerror.Game;

/// <summary>
/// Makes room for extra name lines in a tooltip header.
/// </summary>
/// <remarks>
/// The game measures a tooltip's description block from its text but gives the name a fixed
/// two-line region, so extra lines written into the name render over the row beneath it. The addon
/// is reused between tooltips and keeps whatever geometry we leave behind, so each pass starts by
/// putting every node we touched back the way the game had it. Without that our own growth becomes
/// the baseline for the next calculation and the numbers drift until nothing moves at all.
///
/// A node remembered from the last pass is only written to if it is still reachable from the
/// addon's root this pass. The addon rebuilds parts of its tree between tooltips - gear carries
/// sections a potion does not - so a pointer kept from the last pass can name freed memory, and
/// writing a remembered height into it corrupts the game's heap.
/// </remarks>
public sealed unsafe class TooltipHeaderExpander
{
    /// <summary>
    /// A tooltip header past this is already nonsense, and the growth that produced it would wrap
    /// the ushort it is stored in.
    /// </summary>
    private const int MaxHeight = 2000;

    private readonly Configuration config;
    private readonly TooltipForensics forensics;
    private readonly Dictionary<nint, NodeState> touched = new();
    private readonly HashSet<nint> live = new();

    public TooltipHeaderExpander(Configuration config, TooltipForensics forensics)
    {
        this.config = config;
        this.forensics = forensics;
    }

    public void Expand(AtkUnitBase* unit, string? appendedText, bool log = false)
    {
        if (unit == null)
        {
            Forget();
            return;
        }

        RestoreTouched(unit);

        if (string.IsNullOrEmpty(appendedText))
        {
            if (log)
                Plugin.Log.Information("Header expand: no appended text");

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

        // Every node is back to the game's own layout, so this really is the pristine height.
        var pristine = node->AtkResNode.Height;

        // Ask the game how tall the text draws - counting newlines misses word wrapping and assumes
        // the line advance equals LineSpacing. The line count stays as a floor in case the
        // measurement comes back clipped to the node.
        ushort drawWidth = 0;
        ushort drawHeight = 0;
        node->GetTextDrawSize(&drawWidth, &drawHeight);

        var lines = Math.Max(CountLines(node->NodeText.ToString()), Lines(appendedText).Length + 1);
        var content = Math.Max(drawHeight, lines * lineSpacing);
        var padding = pristine % lineSpacing;
        var topOffset = config.TooltipNameTopOffset;

        // Moving the text up needs no extra height, only moving it down does.
        var target = content + padding + Math.Max(0, topOffset) + config.TooltipNameExtraSpace;
        var delta = target - pristine;

        forensics.Write(
            $"expand node={node->AtkResNode.NodeId} pristine={pristine} spacing={lineSpacing} " +
            $"lines={lines} drawn={drawWidth}x{drawHeight} target={target} delta={delta}");

        if (log)
        {
            Plugin.Log.Information(
                $"Header expand: pristine={pristine} lineSpacing={lineSpacing} lines={lines} " +
                $"drawn={drawWidth}x{drawHeight} padding={padding} space={config.TooltipNameExtraSpace} " +
                $"topOffset={topOffset} target={target} delta={delta}");
        }

        if (delta > 0)
            GrowAncestors(unit, (AtkResNode*)node, delta);

        if (topOffset != 0)
        {
            Capture((AtkResNode*)node);
            node->AtkResNode.SetYFloat(node->AtkResNode.Y + topOffset);
        }

        RecordApplied();

        if (log)
        {
            var parent = node->AtkResNode.ParentNode;
            Plugin.Log.Information(
                $"Header expand applied: node={node->AtkResNode.Height}h y={node->AtkResNode.Y} " +
                $"parent={(parent == null ? 0 : parent->Height)}h touched={touched.Count}");
        }
    }

    /// <summary>
    /// Puts every node back and stops tracking them. Use when the addon still exists.
    /// </summary>
    public void Restore(AtkUnitBase* unit)
    {
        if (unit == null)
            Forget();
        else
            RestoreTouched(unit);
    }

    /// <summary>
    /// Drops the tracked nodes without touching them, for when the addon has been freed.
    /// </summary>
    public void Forget() => touched.Clear();

    /// <summary>
    /// Undoes only the nodes still holding the values we gave them. The game relays out parts of the
    /// tooltip per item - the description block resizes with its text - and putting our remembered
    /// values back over a fresh layout would corrupt it. A node no longer reachable from the root
    /// has been freed and must not be written to at all.
    /// </summary>
    private void RestoreTouched(AtkUnitBase* unit)
    {
        CollectLive(unit);

        foreach (var (key, state) in touched)
        {
            if (!live.Contains(key))
            {
                forensics.Write($"restore skipped: node {key:x} left the tree");
                continue;
            }

            var node = (AtkResNode*)key;
            if (node->Height != state.AppliedHeight || Math.Abs(node->Y - state.AppliedY) > 0.5f)
                continue;

            node->SetHeight(state.PristineHeight);
            node->SetYFloat(state.PristineY);
        }

        touched.Clear();
    }

    /// <summary>
    /// The set of nodes the addon can reach right now. Walking from the root only ever dereferences
    /// nodes the game still owns, which is what makes checking a remembered pointer against it safe.
    /// </summary>
    private void CollectLive(AtkUnitBase* unit)
    {
        live.Clear();
        Walk(unit->RootNode, 0);
    }

    private void Walk(AtkResNode* node, int depth)
    {
        if (depth > 32)
            return;

        for (; node != null; node = node->PrevSiblingNode)
        {
            if (!live.Add((nint)node))
                return;

            Walk(node->ChildNode, depth + 1);
        }
    }

    /// <summary>
    /// Records what we ended up leaving on each node, so the next pass can tell our own changes
    /// apart from a fresh layout by the game.
    /// </summary>
    private void RecordApplied()
    {
        foreach (var key in new List<nint>(touched.Keys))
        {
            var node = (AtkResNode*)key;
            touched[key] = touched[key] with { AppliedHeight = node->Height, AppliedY = node->Y };
        }
    }

    private void Capture(AtkResNode* node)
    {
        var key = (nint)node;
        if (!touched.ContainsKey(key))
            touched[key] = new NodeState(node->Height, node->Y, node->Height, node->Y);
    }

    private void GrowAncestors(AtkUnitBase* unit, AtkResNode* node, int delta)
    {
        var current = node;
        while (current != null && live.Contains((nint)current))
        {
            var parent = current->ParentNode;
            var top = current->Y;

            if (!Grow(current, delta))
                return;

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
    private void AdjustSiblings(AtkResNode* parent, AtkResNode* grown, float top, int delta)
    {
        var parentHeight = parent->Height;

        for (var child = parent->ChildNode; child != null; child = child->PrevSiblingNode)
        {
            if (child == grown || !live.Contains((nint)child))
                continue;

            if (child->Y > top)
            {
                Capture(child);
                child->SetYFloat(child->Y + delta);
                continue;
            }

            if (child->Y <= 0 && child->Y + child->Height >= parentHeight)
                Grow(child, delta);
        }
    }

    /// <summary>
    /// Grows one node, refusing a result that could only come from growth stacking up across passes.
    /// The height is a ushort, so letting it run would wrap it rather than merely look wrong.
    /// </summary>
    private bool Grow(AtkResNode* node, int delta)
    {
        var target = node->Height + delta;
        if (target > MaxHeight)
        {
            forensics.Write($"grow refused: node {node->NodeId} would reach {target}");
            return false;
        }

        Capture(node);
        node->SetHeight((ushort)target);
        return true;
    }

    /// <summary>
    /// Reports why the tail did not match any node, since that is invisible from the node dump.
    /// </summary>
    private static void LogMissingNode(AtkUnitBase* unit, string appendedText)
    {
        Plugin.Log.Information(
            $"Header expand: no node holds the appended lines ({appendedText.Length} chars)");

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

    private readonly record struct NodeState(ushort PristineHeight, float PristineY, ushort AppliedHeight, float AppliedY);
}
