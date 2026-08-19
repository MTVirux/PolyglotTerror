using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PolyglotTerror.Core;

namespace PolyglotTerror.Game;

/// <summary>
/// Appends extra language lines to the item tooltip's backing string array before the game
/// lays the window out, so the game grows the tooltip around the taller text by itself.
/// </summary>
public sealed unsafe class ItemTooltipDecorator : IDisposable
{
    private const string AddonName = "ItemDetail";

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly TooltipSlot nameSlot = new();
    private readonly TooltipSlot categorySlot = new();
    private readonly TooltipSlot descriptionSlot = new();

    public ItemTooltipDecorator(Configuration config, NameCatalog names)
    {
        this.config = config;
        this.names = names;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void Dispose()
        => Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);

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

        if (config.ShowItemName)
            nameSlot.Decorate(data, itemId, client.Name, head => Compose(head, itemId, static item => item.Name));

        if (config.ShowItemCategory && client.Category is not null)
            categorySlot.Decorate(data, itemId, client.Category, head => Compose(head, itemId, static item => item.Category));

        if (config.ShowItemDescription && client.Description is not null)
            descriptionSlot.Decorate(data, itemId, client.Description, head => Compose(head, itemId, static item => item.Description));
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
