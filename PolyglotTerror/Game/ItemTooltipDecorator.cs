using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PolyglotTerror.Core;

namespace PolyglotTerror.Game;

/// <summary>
/// Appends extra language lines to the item tooltip's backing string array before the game lays
/// the window out. Only the blocks the game measures from their own text belong here - it grows
/// those on its own, so nothing has to touch the tooltip's structure. The name is not one of them:
/// it gets a fixed two-line region and is handled by <see cref="ItemTooltipNameNode"/> instead.
/// </summary>
public sealed unsafe class ItemTooltipDecorator : IDisposable
{
    private const string AddonName = "ItemDetail";

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly AddonInspector inspector;
    private readonly TooltipForensics forensics;
    private readonly TooltipSlot categorySlot;
    private readonly TooltipSlot descriptionSlot;
    private DumpStage dump;

    public ItemTooltipDecorator(Configuration config, NameCatalog names, AddonInspector inspector, TooltipForensics forensics)
    {
        this.config = config;
        this.names = names;
        this.inspector = inspector;
        this.forensics = forensics;
        categorySlot = new TooltipSlot(forensics, "category");
        descriptionSlot = new TooltipSlot(forensics, "description");

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnPostRequestedUpdate);
    }

    private enum DumpStage
    {
        Idle,
        Armed,
        AwaitingLayout,
    }

    /// <summary>
    /// Logs the next tooltip's strings and resulting node geometry. Typing a command dismisses the
    /// tooltip, so the dump has to be armed first and fire on the next hover.
    /// </summary>
    public void ArmDump() => dump = DumpStage.Armed;

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnPreRequestedUpdate, OnPostRequestedUpdate);
    }

    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (!config.DecorateTooltip || args is not AddonRequestedUpdateArgs update)
            return;

        var itemId = (uint)Plugin.GameGui.HoveredItem;
        forensics.Write($"pre: item={itemId}");
        if (itemId == 0)
            return;

        var arrays = (StringArrayData**)update.StringArrayData;
        if (arrays == null)
            return;

        var data = arrays[(int)StringArrayType.ItemDetail];
        if (data == null || data->StringArray == null)
            return;

        var client = names.GetItem(NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage), itemId);
        forensics.Write($"pre: client name={(client.Name is null ? "null" : client.Name.Length.ToString())}");
        if (client.Name is null)
            return;

        if (dump == DumpStage.Armed)
        {
            Plugin.Log.Information($"ItemDetail strings before decorating item {itemId}:");
            inspector.DumpTooltipStrings(data);
        }

        if (config.ShowItemCategory && client.Category is not null)
            categorySlot.Decorate(data, itemId, client.Category, head => Compose(head, itemId, static item => item.Category));

        if (config.ShowItemDescription && client.Description is not null)
            descriptionSlot.Decorate(data, itemId, client.Description, head => Compose(head, itemId, static item => item.Description));

        forensics.Write("pre: done");

        if (dump != DumpStage.Armed)
            return;

        Plugin.Log.Information("ItemDetail strings after decorating:");
        inspector.DumpTooltipStrings(data);
        dump = DumpStage.AwaitingLayout;
    }

    private void OnPostRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (dump != DumpStage.AwaitingLayout)
            return;

        dump = DumpStage.Idle;
        inspector.DumpNodes(AddonName, (AtkUnitBase*)args.Addon.Address);
    }

    private string? Compose(string head, uint itemId, Func<ItemNames, string?> pick)
    {
        var candidates = new Dictionary<GameLanguage, string?>();
        foreach (var entry in config.Languages)
        {
            if (entry.Enabled)
                candidates[entry.Language] = pick(names.GetItem(entry.Language, itemId));
        }

        return LineComposer.Compose(head, candidates, config.Languages, config.HideDuplicates);
    }
}
