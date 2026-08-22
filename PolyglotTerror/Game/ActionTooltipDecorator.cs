using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PolyglotTerror.Core;

namespace PolyglotTerror.Game;

/// <summary>
/// Appends extra language lines to the action tooltip's backing string array. Covers everything the
/// game routes through ActionDetail: combat actions and traits, general and main commands,
/// chocobo actions, mounts, minions and fashion accessories.
/// </summary>
public sealed unsafe class ActionTooltipDecorator : IDisposable
{
    private const string AddonName = "ActionDetail";

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly TooltipSlot nameSlot;
    private readonly TooltipSlot descriptionSlot;

    public ActionTooltipDecorator(Configuration config, NameCatalog names)
    {
        this.config = config;
        this.names = names;
        nameSlot = new TooltipSlot();
        descriptionSlot = new TooltipSlot();

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void Dispose()
        => Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);

    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (!config.DecorateActionTooltip || args is not AddonRequestedUpdateArgs update)
            return;

        var hovered = Plugin.GameGui.HoveredAction;
        if (hovered.ActionId == 0)
            return;

        var arrays = (StringArrayData**)update.StringArrayData;
        if (arrays == null)
            return;

        var data = arrays[(int)StringArrayType.ActionDetail];
        if (data == null || data->StringArray == null)
            return;

        var clientLanguage = NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage);
        var kind = hovered.DetailKind;

        // An upgraded action is hovered by its own id, but a few surfaces report only the base one.
        var actionId = hovered.ActionId;
        var client = names.GetActionDetail(clientLanguage, kind, actionId);
        if (client.Name is null && hovered.BaseActionId != 0)
        {
            actionId = hovered.BaseActionId;
            client = names.GetActionDetail(clientLanguage, kind, actionId);
        }

        if (client.Name is null)
            return;

        if (config.ShowActionName)
            nameSlot.Decorate(data, actionId, client.Name, head => Compose(head, kind, actionId, static action => action.Name));

        if (config.ShowActionDescription && client.Description is not null)
            descriptionSlot.Decorate(data, actionId, client.Description, head => Compose(head, kind, actionId, static action => action.Description));
    }

    private string? Compose(string head, DetailKind kind, uint actionId, Func<ActionNames, string?> pick)
    {
        var candidates = new Dictionary<GameLanguage, string?>();
        foreach (var entry in config.Languages)
        {
            if (entry.Enabled)
                candidates[entry.Language] = pick(names.GetActionDetail(entry.Language, kind, actionId));
        }

        return LineComposer.Compose(head, candidates, config.Languages, config.HideDuplicates);
    }
}
