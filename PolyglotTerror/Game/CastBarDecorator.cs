using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PolyglotTerror.Core;

namespace PolyglotTerror.Game;

/// <summary>
/// Rewrites cast bar text nodes on every draw, because the game restores its own text each frame.
/// </summary>
public sealed unsafe class CastBarDecorator : IDisposable
{
    public const string OverheadAddonName = "CastBarEnemy";
    public const string PartyListAddonName = "_PartyList";

    private const float LineLift = 0f;

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly NodeTextWriter writer = new();
    private readonly Dictionary<string, CastBarSurface> surfaces = new();
    private readonly Dictionary<string, uint> discoveredNodeIds = new();
    private readonly Dictionary<nint, string> lastWritten = new();
    private readonly Dictionary<string, Dictionary<nint, NodeState>> touched = new();
    private bool overheadRegistered;
    private bool partyListRegistered;

    public CastBarDecorator(Configuration config, NameCatalog names)
    {
        this.config = config;
        this.names = names;
    }

    public void Register(CastBarSurface surface)
    {
        if (!surfaces.TryAdd(surface.AddonName, surface))
            return;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, surface.AddonName, OnDraw);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, surface.AddonName, OnDraw);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, surface.AddonName, OnFinalize);
    }

    /// <summary>
    /// Registers the cast bars drawn above enemies' heads. They live in their own addon with a
    /// typed node per bar, so they need no node id and no surface entry.
    /// </summary>
    public void RegisterOverheadBars()
    {
        if (overheadRegistered)
            return;

        overheadRegistered = true;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, OverheadAddonName, OnOverheadDraw);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, OverheadAddonName, OnOverheadDraw);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OverheadAddonName, OnFinalize);
    }

    /// <summary>
    /// Registers the party list, whose cast text sits inside a component per member and so is
    /// found by walking the tree rather than by node id.
    /// </summary>
    public void RegisterPartyList()
    {
        if (partyListRegistered)
            return;

        partyListRegistered = true;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, PartyListAddonName, OnPartyListDraw);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, PartyListAddonName, OnPartyListDraw);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, PartyListAddonName, OnFinalize);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnDraw, OnOverheadDraw, OnPartyListDraw, OnFinalize);
        RestoreTouchedNodes();
        surfaces.Clear();
        discoveredNodeIds.Clear();
        lastWritten.Clear();
        touched.Clear();
        writer.Dispose();
    }

    /// <summary>
    /// Drops what we know about an addon's nodes, since they are freed right after this event and
    /// their pointers must not be touched again.
    /// </summary>
    private void OnFinalize(AddonEvent type, AddonArgs args)
    {
        if (!touched.Remove(args.AddonName, out var nodes))
            return;

        foreach (var key in nodes.Keys)
        {
            lastWritten.Remove(key);
            writer.Forget(key);
        }
    }

    /// <summary>
    /// Puts every node we moved back where the game had it, so unloading the plugin leaves the
    /// cast bars as they were.
    /// </summary>
    private void RestoreTouchedNodes()
    {
        foreach (var nodes in touched.Values)
        {
            foreach (var (key, state) in nodes)
            {
                var node = (AtkTextNode*)key;
                node->AtkResNode.SetYFloat(state.Y);
                node->AtkResNode.SetHeight(state.Height);
                node->TextFlags = state.Flags;
            }
        }
    }

    private void OnDraw(AddonEvent type, AddonArgs args)
    {
        if (!surfaces.TryGetValue(args.AddonName, out var surface))
            return;

        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible)
            return;

        if (ResolveCaster(surface.Source) is not { IsCasting: true } caster)
            return;

        var actionId = caster.CastActionId;
        var clientName = ClientActionName(actionId);
        if (clientName == null)
            return;

        var node = ResolveTextNode(addon, surface, clientName);
        if (node != null)
            Decorate(args.AddonName, node, actionId, surface.Policy);
    }

    private void OnOverheadDraw(AddonEvent type, AddonArgs args)
    {
        var addon = (FFXIVClientStructs.FFXIV.Client.UI.AddonCastBarEnemy*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible)
            return;

        var bars = addon->CastBarNodes;
        for (var i = 0; i < bars.Length; i++)
        {
            var node = bars[i].CastNameTextNode;
            var container = bars[i].CastBarNode;
            if (node == null || container == null || !container->NodeFlags.HasFlag(NodeFlags.Visible))
                continue;

            if (Plugin.ObjectTable.SearchById(bars[i].ObjectId.Id) is not IBattleChara { IsCasting: true } caster)
                continue;

            var actionId = caster.CastActionId;
            var clientName = ClientActionName(actionId);
            if (clientName == null || !HoldsActionName(node, clientName))
                continue;

            Decorate(args.AddonName, node, actionId, LanguagePolicy.FullStack);
        }
    }

    private void OnPartyListDraw(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null || !addon->IsVisible)
            return;

        foreach (var member in Plugin.PartyList)
        {
            if (member.GameObject is not IBattleChara { IsCasting: true } caster)
                continue;

            var actionId = caster.CastActionId;
            var clientName = ClientActionName(actionId);
            if (clientName == null)
                continue;

            var node = FindTextNode(addon->UldManager, clientName);
            if (node != null)
                Decorate(args.AddonName, node, actionId, LanguagePolicy.PrimaryOnly);
        }
    }

    private AtkTextNode* FindTextNode(AtkUldManager manager, string clientName)
    {
        for (var i = 0; i < manager.NodeListCount; i++)
        {
            var node = manager.NodeList[i];
            if (node == null)
                continue;

            if (node->Type == NodeType.Text)
            {
                var text = (AtkTextNode*)node;
                if (HoldsActionName(text, clientName))
                    return text;

                continue;
            }

            if ((ushort)node->Type < 1000)
                continue;

            var component = ((AtkComponentNode*)node)->Component;
            if (component == null)
                continue;

            var found = FindTextNode(component->UldManager, clientName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void Decorate(string addonName, AtkTextNode* node, uint actionId, LanguagePolicy policy)
    {
        var original = Remember(addonName, node);

        if (policy == LanguagePolicy.PrimaryOnly)
        {
            var primary = LineComposer.Primary(config.Languages);
            var name = primary is { } language ? names.GetAction(language, actionId) : null;
            if (!string.IsNullOrEmpty(name))
                Write(node, name);

            return;
        }

        var resolved = new Dictionary<GameLanguage, string?>();
        foreach (var entry in config.Languages)
        {
            if (entry.Enabled)
                resolved[entry.Language] = names.GetAction(entry.Language, actionId);
        }

        var composed = LineComposer.ComposeStandalone(resolved, config.Languages, config.HideDuplicates);
        if (composed == null)
            return;

        Write(node, composed);
        var lines = composed.AsSpan().Count(LineComposer.Separator) + 1;
        if (lines > 1)
        {
            node->TextFlags |= TextFlags.MultiLine | TextFlags.WordWrap;
            FitLines(node, lines);
        }

        OffsetText(node, original.Y, lines);
    }

    /// <summary>
    /// Grows the node to fit every line, since the game sizes it for the single line it expects.
    /// </summary>
    private static void FitLines(AtkTextNode* node, int lines)
    {
        var spacing = node->LineSpacing > 0 ? node->LineSpacing : node->FontSize + 4;
        node->AtkResNode.SetHeight((ushort)(lines * spacing));
    }

    /// <summary>
    /// Lifts the text by <see cref="LineLift"/> for each line shown, since the node grows downwards.
    /// A single line still sits where the game put it, so only the configured offset moves it. Both
    /// move from the game's own position, so the offset stays absolute instead of piling up frame
    /// after frame.
    /// </summary>
    private void OffsetText(AtkTextNode* node, float anchor, int lines)
    {
        var lift = lines > 1 ? lines * LineLift : 0f;
        node->AtkResNode.SetYFloat(anchor + lift + config.CastBarTextOffset);
    }

    /// <summary>
    /// Records how the game had a node the first time we touch it, so we can put it back later.
    /// </summary>
    private NodeState Remember(string addonName, AtkTextNode* node)
    {
        if (!touched.TryGetValue(addonName, out var nodes))
            touched[addonName] = nodes = new Dictionary<nint, NodeState>();

        var key = (nint)node;
        if (!nodes.TryGetValue(key, out var state))
        {
            state = new NodeState(node->AtkResNode.Y, node->AtkResNode.Height, node->TextFlags);
            nodes[key] = state;
        }

        return state;
    }

    private static IBattleChara? ResolveCaster(CastSource source) => source switch
    {
        CastSource.Self => Plugin.ObjectTable.LocalPlayer,
        CastSource.Target => Plugin.TargetManager.Target as IBattleChara,
        CastSource.FocusTarget => Plugin.TargetManager.FocusTarget as IBattleChara,
        _ => null,
    };

    private string? ClientActionName(uint actionId)
    {
        var language = NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage);
        var name = names.GetAction(language, actionId);
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private void Write(AtkTextNode* node, string text)
    {
        writer.Write(node, text);
        lastWritten[(nint)node] = text;
    }

    /// <summary>
    /// Finds the node holding the action name, preferring a cached id and falling back to a scan.
    /// A wrong or stale hardcoded id then costs us a frame instead of writing into an unrelated node.
    /// </summary>
    private AtkTextNode* ResolveTextNode(AtkUnitBase* addon, CastBarSurface surface, string clientName)
    {
        if (discoveredNodeIds.TryGetValue(surface.AddonName, out var cachedId))
            return GetTextNode(addon, cachedId);

        if (surface.TextNodeId != 0)
        {
            var hinted = GetTextNode(addon, surface.TextNodeId);
            if (hinted != null && HoldsActionName(hinted, clientName))
            {
                discoveredNodeIds[surface.AddonName] = surface.TextNodeId;
                return hinted;
            }
        }

        var count = addon->UldManager.NodeListCount;
        for (var i = 0; i < count; i++)
        {
            var candidate = addon->UldManager.NodeList[i];
            if (candidate == null || candidate->Type != NodeType.Text)
                continue;

            var text = (AtkTextNode*)candidate;
            if (!HoldsActionName(text, clientName))
                continue;

            discoveredNodeIds[surface.AddonName] = candidate->NodeId;
            return text;
        }

        return null;
    }

    private static AtkTextNode* GetTextNode(AtkUnitBase* addon, uint nodeId)
    {
        var node = addon->GetNodeById(nodeId);
        return node == null || node->Type != NodeType.Text ? null : (AtkTextNode*)node;
    }

    private readonly record struct NodeState(float Y, ushort Height, TextFlags Flags);

    // Once we have written to a node the game's plain name is gone, so our own text counts as a match.
    private bool HoldsActionName(AtkTextNode* node, string clientName)
    {
        var text = node->NodeText.ToString().Trim();
        if (string.Equals(text, clientName, StringComparison.Ordinal))
            return true;

        return lastWritten.TryGetValue((nint)node, out var written)
            && string.Equals(text, written, StringComparison.Ordinal);
    }
}
