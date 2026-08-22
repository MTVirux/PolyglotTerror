using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PolyglotTerror.Core;
using PolyglotTerror.Windows;

namespace PolyglotTerror.Game;

/// <summary>The panel of translated names beside the item tooltip.</summary>
public sealed unsafe class ItemTooltipNames : TooltipNamePanel
{
    // Rows of the game's own UI text sheet, so the panel's labels follow the client's language.
    private const uint NameLabel = 1898;
    private const uint CategoryLabel = 7871;
    private const uint DescriptionLabel = 543;

    private readonly AddonInspector inspector;

    private uint itemId;
    private ItemNames client = new(null, null, null);
    private bool dumpArmed;

    public ItemTooltipNames(
        Configuration config,
        NameCatalog names,
        AddonInspector inspector,
        NamePanelWindow window)
        : base(config, names, window, "ItemDetail")
        => this.inspector = inspector;

    protected override bool Wanted => Config.DecorateTooltip;

    protected override bool CycleWithScroll => Config.CycleLanguagesWithScroll;

    protected override int Gap => Config.TooltipPanelGap;

    protected override int OffsetY => Config.TooltipPanelOffsetY;

    /// <summary>
    /// Dumps the next tooltip's nodes. Typing a command dismisses the tooltip, so it has to be armed
    /// first and fire on the next hover.
    /// </summary>
    public void ArmDump() => dumpArmed = true;

    protected override bool TryResolveSubject()
    {
        itemId = (uint)Plugin.GameGui.HoveredItem;
        if (itemId == 0)
            return false;

        client = Names.GetItem(ClientLanguage, itemId);
        return client.Name is not null;
    }

    protected override NameLine[] ComposeOne(GameLanguage language)
    {
        var other = Names.GetItem(language, itemId);
        var block = new List<NameLine>();

        Add(block, NameLabel, Config.ShowItemName, other.Name, client.Name);
        Add(block, CategoryLabel, Config.ShowItemCategory, other.Category, client.Category);
        Add(block, DescriptionLabel, Config.ShowItemDescription, other.Description, client.Description);

        return block.ToArray();
    }

    protected override void OnUpdated(AddonArgs args)
    {
        if (!dumpArmed)
            return;

        dumpArmed = false;
        inspector.DumpNodes("ItemDetail", (AtkUnitBase*)args.Addon.Address);
    }
}
