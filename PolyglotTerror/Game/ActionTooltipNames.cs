using System.Collections.Generic;
using Dalamud.Game.Gui;
using PolyglotTerror.Core;
using PolyglotTerror.Windows;

namespace PolyglotTerror.Game;

/// <summary>
/// The panel of translated names beside the action tooltip. Covers everything the game routes
/// through ActionDetail: combat actions and traits, general and main commands, chocobo actions,
/// mounts, minions and fashion accessories.
/// </summary>
public sealed class ActionTooltipNames : TooltipNamePanel
{
    private DetailKind kind;
    private uint actionId;
    private SubjectNames client = new(null, null, null);

    public ActionTooltipNames(Configuration config, NameCatalog names, NamePanelWindow window)
        : base(config, names, window, "ActionDetail")
    {
    }

    protected override bool Wanted => Config.DecorateActionTooltip;

    protected override int Gap => Config.ActionPanelGap;

    protected override int OffsetY => Config.ActionPanelOffsetY;

    protected override bool TryResolveSubject()
    {
        var hovered = Plugin.GameGui.HoveredAction;
        if (hovered.ActionId == 0)
            return false;

        kind = hovered.DetailKind;
        actionId = hovered.ActionId;
        client = Names.GetActionDetail(ClientLanguage, kind, actionId);

        // An upgraded action is hovered by its own id, but a few surfaces report only the base one.
        if (client.Name is null && hovered.BaseActionId != 0)
        {
            actionId = hovered.BaseActionId;
            client = Names.GetActionDetail(ClientLanguage, kind, actionId);
        }

        return client.Name is not null;
    }

    protected override NameLine[] ComposeOne(GameLanguage language)
    {
        var other = Names.GetActionDetail(language, kind, actionId);
        var block = new List<NameLine>();

        AddName(block, Config.ShowActionName, other.Name, client.Name);
        Add(block, CategoryLabel, Config.ShowActionCategory, other.Category, client.Category);
        Add(block, DescriptionLabel, Config.ShowActionDescription, other.Description, client.Description);

        return block.ToArray();
    }
}
