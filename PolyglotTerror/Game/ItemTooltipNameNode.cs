using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using PolyglotTerror.Core;

namespace PolyglotTerror.Game;

/// <summary>
/// Shows an item's other-language names in a text node of our own, parked under everything the game
/// drew, and grows the tooltip frame to cover it.
/// </summary>
/// <remarks>
/// This is the only thing in the plugin that changes a tooltip's structure. Extra lines cannot go
/// into the name the game draws: it gets a fixed two-line region, so making room there means
/// relaying the header out, moving every row beneath it and stretching each frame around them.
/// Appending our own node instead leaves the game's layout untouched, and the only geometry we
/// write is the height of the frame that wraps it.
///
/// KamiToolKit owns the node - creation, attachment and teardown - so nothing here allocates or
/// frees game memory. The frame nodes are re-derived from the addon on every pass rather than
/// remembered, because a pointer kept from the last tooltip can name memory the game has freed.
/// </remarks>
public sealed unsafe class ItemTooltipNameNode : IDisposable
{
    private const string AddonName = "ItemDetail";
    private const uint NameNodeId = 900_501;
    private const float SidePadding = 16f;
    private const float BottomPadding = 8f;

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly TooltipForensics forensics;
    private readonly AddonController<AddonItemDetail> controller;
    private readonly List<Growth> grown = new();

    private TextNode? node;
    private bool enabled;
    private bool disposed;

    public ItemTooltipNameNode(Configuration config, NameCatalog names, TooltipForensics forensics)
    {
        this.config = config;
        this.names = names;
        this.forensics = forensics;

        controller = new AddonController<AddonItemDetail>
        {
            AddonName = AddonName,
            OnSetup = HandleSetup,
            OnFinalize = HandleFinalize,
        };
    }

    public bool Enabled => enabled;

    /// <summary>
    /// Must be called on the framework thread - KamiToolKit asserts on it whenever it touches
    /// an addon.
    /// </summary>
    public void SetEnabled(bool value)
    {
        if (disposed || value == enabled)
            return;

        enabled = value;

        if (value)
        {
            controller.Enable();

            var live = LiveAddon();
            if (live != null && node == null)
                HandleSetup(live);

            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, HandleRequestedUpdate);
        }
        else
        {
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, HandleRequestedUpdate);
            TearDownNow();
            controller.Disable();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        if (enabled)
        {
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, HandleRequestedUpdate);
            TearDownNow();
        }

        controller.Dispose();
    }

    private static AddonItemDetail* LiveAddon()
        => (AddonItemDetail*)Plugin.GameGui.GetAddonByName(AddonName).Address;

    private void TearDownNow()
    {
        var addon = LiveAddon();
        if (addon != null)
            HandleFinalize(addon);
        else
            DropNode();
    }

    private void HandleSetup(AddonItemDetail* addon)
    {
        if (node != null || addon == null || addon->RootNode == null)
            return;

        var text = new TextNode
        {
            NodeId = NameNodeId,
            Size = new Vector2(addon->RootNode->Width - (SidePadding * 2f), 0f),
            FontSize = 12,
            AlignmentType = AlignmentType.TopLeft,
            TextColor = new Vector4(0.85f, 0.85f, 0.75f, 1f),
            TextOutlineColor = new Vector4(0f, 0f, 0f, 1f),
            TextFlags = TextFlags.Edge | TextFlags.MultiLine | TextFlags.WordWrap,
            IsVisible = false,
        };

        // Decoration only - it must stay out of the tooltip's input handling.
        text.RemoveNodeFlags(NodeFlags.RespondToMouse, NodeFlags.EmitsEvents, NodeFlags.HasCollision);
        text.AttachNode(addon->RootNode, NodePosition.AsLastChild);
        node = text;

        forensics.Write("name node: attached");
    }

    private void HandleFinalize(AddonItemDetail* addon)
    {
        // The addon is going away, so its frame must not be left carrying our height.
        Shrink(addon);
        DropNode();
    }

    private void DropNode()
    {
        node?.Dispose();
        node = null;
        grown.Clear();
    }

    private void HandleRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonItemDetail*)args.Addon.Address;
        if (addon == null || node == null || addon->RootNode == null)
            return;

        forensics.Write("name node: begin");
        Shrink(addon);

        var lines = ComposeLines();
        if (lines.Length == 0)
        {
            node.IsVisible = false;
            forensics.Write("name node: nothing to show");
            return;
        }

        var root = addon->RootNode;

        // Everything the game drew ends at the root's height, so that is where ours starts.
        var contentBottom = root->Height;
        var width = root->Width - (SidePadding * 2f);

        node.String = string.Join('\n', lines);
        node.Size = new Vector2(width, 0f);

        var drawn = node.GetTextDrawSize(false);
        var delta = (int)(drawn.Y + BottomPadding + config.TooltipNameExtraSpace);

        node.Size = new Vector2(width, drawn.Y);
        node.Position = new Vector2(SidePadding, contentBottom - BottomPadding);
        node.IsVisible = true;

        forensics.Write($"name node: lines={lines.Length} drawn={drawn.X}x{drawn.Y} bottom={contentBottom} delta={delta}");

        Grow(addon, delta);

        forensics.Write($"name node: done, frame nodes={grown.Count}");
    }

    /// <summary>
    /// Puts the frame back to the height the game gave it. Only the nodes still carrying the value
    /// we left are touched - the game relays the tooltip out per item, and writing a remembered
    /// height over a fresh layout would corrupt it.
    /// </summary>
    private void Shrink(AddonItemDetail* addon)
    {
        if (grown.Count == 0)
            return;

        var frame = FrameNodes(addon);
        if (frame.Count == grown.Count)
        {
            for (var i = 0; i < frame.Count; i++)
            {
                var target = (AtkResNode*)frame[i];
                if (target->Height == grown[i].Applied)
                    target->SetHeight(grown[i].Pristine);
            }
        }
        else
        {
            forensics.Write($"name node: frame changed shape ({grown.Count} -> {frame.Count}), leaving it alone");
        }

        grown.Clear();
    }

    private void Grow(AddonItemDetail* addon, int delta)
    {
        grown.Clear();
        if (delta <= 0)
            return;

        foreach (var key in FrameNodes(addon))
        {
            var target = (AtkResNode*)key;
            var pristine = target->Height;
            target->SetHeight((ushort)(pristine + delta));
            grown.Add(new Growth(pristine, target->Height));
        }
    }

    /// <summary>
    /// The nodes that draw the tooltip's outer frame, in a fixed order so a later pass can line its
    /// remembered heights up with them. Derived fresh every time rather than cached.
    /// </summary>
    private static List<nint> FrameNodes(AddonItemDetail* addon)
    {
        var nodes = new List<nint>();
        if (addon == null)
            return nodes;

        if (addon->RootNode != null)
            nodes.Add((nint)addon->RootNode);

        var window = addon->WindowNode;
        if (window != null)
        {
            nodes.Add((nint)window);

            // The frame is a component, so its own background parts have to grow with it or the
            // border stops short of our text.
            if (window->Component != null)
            {
                var manager = &window->Component->UldManager;
                for (var i = 0; i < manager->NodeListCount; i++)
                {
                    var child = manager->NodeList[i];
                    if (child != null && child->Type == NodeType.NineGrid)
                        nodes.Add((nint)child);
                }
            }
        }

        if (addon->WindowCollisionNode != null)
            nodes.Add((nint)addon->WindowCollisionNode);

        return nodes;
    }

    private string[] ComposeLines()
    {
        if (!config.DecorateTooltip || !config.ShowItemName)
            return [];

        var itemId = (uint)Plugin.GameGui.HoveredItem;
        if (itemId == 0)
            return [];

        var client = names.GetItem(NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage), itemId);
        if (client.Name is null)
            return [];

        var candidates = new Dictionary<GameLanguage, string?>();
        foreach (var entry in config.Languages)
        {
            if (entry.Enabled)
                candidates[entry.Language] = names.GetItem(entry.Language, itemId).Name;
        }

        var lines = LineComposer.BuildLines(client.Name, candidates, config.Languages, config.HideDuplicates);

        // The first line is the name the game already drew.
        var extra = new List<string>();
        for (var i = 1; i < lines.Count; i++)
            extra.Add(lines[i]);

        return extra.ToArray();
    }

    private readonly record struct Growth(ushort Pristine, ushort Applied);
}
