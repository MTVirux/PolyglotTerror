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

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly NodeTextWriter writer = new();
    private readonly Dictionary<string, CastBarSurface> surfaces = new();
    private readonly Dictionary<string, uint> discoveredNodeIds = new();
    private readonly Dictionary<nint, string> lastWritten = new();
    private bool overheadRegistered;

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
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnDraw, OnOverheadDraw);
        surfaces.Clear();
        discoveredNodeIds.Clear();
        lastWritten.Clear();
        writer.Dispose();
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
            Decorate(node, actionId, surface.Policy);
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

            Decorate(node, actionId, LanguagePolicy.FullStack);
        }
    }

    private void Decorate(AtkTextNode* node, uint actionId, LanguagePolicy policy)
    {
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

        var composed = LineComposer.Compose(null, resolved, config.Languages, config.HideDuplicates);
        if (composed == null)
            return;

        Write(node, composed);
        node->TextFlags |= TextFlags.MultiLine | TextFlags.WordWrap;
        node->AtkResNode.SetHeight((ushort)config.CastBarHeight);
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
