using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PolyglotTerror.Core;

namespace PolyglotTerror.Game;

/// <summary>
/// Appends extra language lines to the item tooltip's backing string array before the game lays
/// the window out. The description block is measured from its text and grows on its own; the name
/// is given a fixed two-line region, so that one needs TooltipHeaderExpander afterwards.
/// </summary>
public sealed unsafe class ItemTooltipDecorator : IDisposable
{
    private const string AddonName = "ItemDetail";

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly AddonInspector inspector;
    private readonly TooltipSlot nameSlot = new();
    private readonly TooltipSlot categorySlot = new();
    private readonly TooltipSlot descriptionSlot = new();
    private readonly TooltipHeaderExpander expander;
    private DumpStage dump;

    public ItemTooltipDecorator(Configuration config, NameCatalog names, AddonInspector inspector)
    {
        this.config = config;
        this.names = names;
        this.inspector = inspector;
        expander = new TooltipHeaderExpander(config);

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnPostRequestedUpdate);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonName, OnFinalize);
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
        Plugin.AddonLifecycle.UnregisterListener(OnPreRequestedUpdate, OnPostRequestedUpdate, OnFinalize);
        expander.Restore();
    }

    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (!config.DecorateTooltip || args is not AddonRequestedUpdateArgs update)
            return;

        var itemId = (uint)Plugin.GameGui.HoveredItem;
        if (itemId == 0)
            return;

        var arrays = (StringArrayData**)update.StringArrayData;
        if (arrays == null)
            return;

        var data = arrays[(int)StringArrayType.ItemDetail];
        if (data == null || data->StringArray == null)
            return;

        var client = names.GetItem(NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage), itemId);
        if (client.Name is null)
            return;

        if (dump == DumpStage.Armed)
        {
            Plugin.Log.Information($"ItemDetail strings before decorating item {itemId}:");
            inspector.DumpTooltipStrings(data);
        }

        if (config.ShowItemName)
            nameSlot.Decorate(data, itemId, client.Name, head => Compose(head, itemId, static item => item.Name));

        if (config.ShowItemCategory && client.Category is not null)
            categorySlot.Decorate(data, itemId, client.Category, head => Compose(head, itemId, static item => item.Category));

        if (config.ShowItemDescription && client.Description is not null)
            descriptionSlot.Decorate(data, itemId, client.Description, head => Compose(head, itemId, static item => item.Description));

        if (dump != DumpStage.Armed)
            return;

        Plugin.Log.Information("ItemDetail strings after decorating:");
        inspector.DumpTooltipStrings(data);
        dump = DumpStage.AwaitingLayout;
    }

    /// <summary>
    /// The nodes are freed right after this, so drop them rather than putting them back.
    /// </summary>
    private void OnFinalize(AddonEvent type, AddonArgs args) => expander.Forget();

    private void OnPostRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (config.ShowItemName && config.ExpandTooltipName)
            expander.Expand(addon, nameSlot.AppendedText, dump == DumpStage.AwaitingLayout);
        else
            expander.Restore();

        if (dump != DumpStage.AwaitingLayout)
            return;

        dump = DumpStage.Idle;
        inspector.DumpNodes(AddonName, addon);
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
