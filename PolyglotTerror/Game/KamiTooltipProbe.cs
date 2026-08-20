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
/// THROWAWAY SPIKE - not part of the plugin's design. Answers whether KamiToolKit gives better
/// control over the item tooltip than rewriting the game's own strings does.
/// </summary>
/// <remarks>
/// Instead of appending lines to the name string and then making room inside the header, this owns
/// a text node parked under everything the game drew and grows the window to cover it. Nothing the
/// game laid out moves, so the only geometry we touch is the outer frame.
/// KamiToolKit's controller handles the node's lifetime; the text still comes from Dalamud's
/// RequestedUpdate event, which the controller has no equivalent of.
/// </remarks>
public sealed unsafe class KamiTooltipProbe : IDisposable
{
    private const string AddonName = "ItemDetail";
    private const uint ProbeNodeId = 900_501;
    private const float SidePadding = 16f;
    private const float BottomPadding = 8f;

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly AddonController<AddonItemDetail> controller;
    private readonly Dictionary<nint, Growth> grown = new();

    private TextNode? node;
    private bool enabled;
    private bool disposed;
    private bool verbose;

    public KamiTooltipProbe(Configuration config, NameCatalog names)
    {
        this.config = config;
        this.names = names;

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

            // The tooltip is normally closed when the toggle is flipped, so there is usually
            // nothing live to replay setup against, but cover the case where it is open.
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

    /// <summary>Logs the geometry of the next tooltip pass.</summary>
    public void ArmLog() => verbose = true;

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
            NodeId = ProbeNodeId,
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
    }

    private void HandleFinalize(AddonItemDetail* addon)
    {
        // The addon is going away, so its own nodes must not be left carrying our sizes.
        Restore();
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
        if (addon == null || node == null)
            return;

        Restore();

        var lines = ComposeLines();
        if (lines.Length == 0)
        {
            node.IsVisible = false;
            return;
        }

        var root = addon->RootNode;
        if (root == null)
            return;

        // Everything the game drew ends at the root's height, so that is where ours starts.
        var contentBottom = root->Height;

        node.String = string.Join('\n', lines);
        node.Size = new Vector2(root->Width - (SidePadding * 2f), 0f);

        var drawn = node.GetTextDrawSize(false);
        var height = drawn.Y + BottomPadding;

        node.Size = new Vector2(root->Width - (SidePadding * 2f), drawn.Y);
        node.Position = new Vector2(SidePadding, contentBottom - BottomPadding);
        node.IsVisible = true;

        Grow(root, (int)height);
        Grow((AtkResNode*)addon->WindowNode, (int)height);
        Grow((AtkResNode*)addon->WindowCollisionNode, (int)height);
        GrowWindowBackground(addon, (int)height);

        Record();

        if (verbose)
        {
            verbose = false;
            Plugin.Log.Information(
                $"Kami probe: lines={lines.Length} drawn={drawn.X}x{drawn.Y} contentBottom={contentBottom} " +
                $"grow={height} root={root->Height}h window={(addon->WindowNode == null ? 0 : addon->WindowNode->AtkResNode.Height)}h " +
                $"touched={grown.Count}");
        }
    }

    /// <summary>
    /// The window frame is a component, so its own background parts have to grow with it or the
    /// tooltip's border stops short of our text.
    /// </summary>
    private void GrowWindowBackground(AddonItemDetail* addon, int delta)
    {
        var window = addon->WindowNode;
        if (window == null || window->Component == null)
            return;

        var manager = &window->Component->UldManager;
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var child = manager->NodeList[i];
            if (child == null || child->Type != NodeType.NineGrid)
                continue;

            Grow(child, delta);
        }
    }

    private void Grow(AtkResNode* target, int delta)
    {
        if (target == null || delta <= 0)
            return;

        var key = (nint)target;
        if (!grown.ContainsKey(key))
            grown[key] = new Growth(target->Height, target->Height);

        target->SetHeight((ushort)(target->Height + delta));
    }

    /// <summary>
    /// Puts back only the nodes still holding the height we gave them. The game re-lays the tooltip
    /// out per item, and writing a remembered height over a fresh layout would corrupt it.
    /// </summary>
    private void Restore()
    {
        foreach (var (key, growth) in grown)
        {
            var target = (AtkResNode*)key;
            if (target->Height == growth.Applied)
                target->SetHeight(growth.Pristine);
        }

        grown.Clear();
    }

    private void Record()
    {
        foreach (var key in new List<nint>(grown.Keys))
            grown[key] = grown[key] with { Applied = ((AtkResNode*)key)->Height };
    }

    private string[] ComposeLines()
    {
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
