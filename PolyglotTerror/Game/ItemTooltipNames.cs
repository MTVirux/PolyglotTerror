using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using PolyglotTerror.Core;
using PolyglotTerror.Windows;

namespace PolyglotTerror.Game;

/// <summary>
/// Keeps <see cref="NamePanelWindow"/> beside the item tooltip and filled with the hovered item's
/// names, one block per language.
/// </summary>
/// <remarks>
/// The tooltip's own nodes are never touched. Content is picked up when the game refreshes the
/// tooltip; the panel is placed and hidden from the framework tick, because a tooltip that is merely
/// hidden stops drawing without telling anyone.
/// </remarks>
public sealed unsafe class ItemTooltipNames : IDisposable
{
    private const string AddonName = "ItemDetail";
    private const int IdleFramesBeforeClose = 10;
    private const float VerticalOffset = 20f;

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly AddonInspector inspector;
    private readonly NamePanelWindow window;

    private List<NameSection> sections = [];
    private GameLanguage? selected;
    private int idleFrames;
    private bool dumpArmed;
    private bool enabled;
    private bool disposed;

    public ItemTooltipNames(
        Configuration config,
        NameCatalog names,
        AddonInspector inspector,
        NamePanelWindow window)
    {
        this.config = config;
        this.names = names;
        this.inspector = inspector;
        this.window = window;
    }

    public bool Enabled => enabled;

    /// <summary>
    /// Dumps the next tooltip's nodes. Typing a command dismisses the tooltip, so it has to be armed
    /// first and fire on the next hover.
    /// </summary>
    public void ArmDump() => dumpArmed = true;

    public void SetEnabled(bool value)
    {
        if (disposed || value == enabled)
            return;

        enabled = value;

        if (value)
        {
            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnRequestedUpdate);
            Plugin.Framework.Update += OnFrameworkUpdate;
        }
        else
        {
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnRequestedUpdate);
            Plugin.Framework.Update -= OnFrameworkUpdate;
            Hide();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        if (enabled)
        {
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnRequestedUpdate);
            Plugin.Framework.Update -= OnFrameworkUpdate;
            Hide();
        }

        Hide();
    }

    private static AtkUnitBase* Tooltip()
        => (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName).Address;

    private void OnRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        Rebuild();

        if (!dumpArmed)
            return;

        dumpArmed = false;
        inspector.DumpNodes(AddonName, (AtkUnitBase*)args.Addon.Address);
    }

    private void Rebuild()
    {
        sections = Compose();
        window.SetSections(sections);
    }

    /// <summary>
    /// The languages the wheel steps through: the enabled ones, minus the client's own, which the
    /// tooltip is already showing.
    /// </summary>
    private List<GameLanguage> Cycle()
    {
        var client = NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage);
        var cycle = new List<GameLanguage>();

        foreach (var entry in config.Languages)
        {
            if (entry.Enabled && entry.Language != client)
                cycle.Add(entry.Language);
        }

        return cycle;
    }

    /// <summary>
    /// Steps the shown language when the wheel moves. The delta is only read, never swallowed, so
    /// whatever is under the cursor still scrolls too.
    /// </summary>
    private void StepOnScroll()
    {
        var wheel = UIInputData.Instance()->CursorInputs.MouseWheel;
        if (wheel == 0)
            return;

        var cycle = Cycle();
        if (cycle.Count == 0)
            return;

        var current = selected is null ? 0 : Math.Max(0, cycle.IndexOf(selected.Value));
        var next = (current + (wheel > 0 ? 1 : -1) + cycle.Count) % cycle.Count;

        selected = cycle[next];
        Rebuild();
    }

    /// <summary>
    /// Holding Alt hides the tooltip, but none of the addon's own visibility fields move when it
    /// does, so the key is read directly rather than inferred from the tooltip.
    /// </summary>
    private static bool AltHeld
        => Plugin.KeyState[VirtualKey.MENU]
           || Plugin.KeyState[VirtualKey.LMENU]
           || Plugin.KeyState[VirtualKey.RMENU];

    private static bool Showing(AtkUnitBase* tooltip)
        => tooltip != null
           && tooltip->IsVisible
           && tooltip->RootNode != null
           && tooltip->RootNode->IsVisible()
           && tooltip->RootNode->Color.A > 0;

    /// <summary>
    /// Follows the tooltip. It moves with the cursor every frame and is hidden rather than closed
    /// between hovers, so neither its position nor its visibility can be learnt from an event.
    /// </summary>
    private void OnFrameworkUpdate(IFramework framework)
    {
        var tooltip = Tooltip();

        if (AltHeld || !Showing(tooltip))
        {
            Idle();
            return;
        }

        if (config.CycleLanguagesWithScroll)
            StepOnScroll();

        if (sections.Count == 0)
        {
            Idle();
            return;
        }

        idleFrames = 0;
        window.Anchor = Beside(tooltip, config.TooltipPanelGap);
        window.IsOpen = true;
    }

    /// <summary>
    /// Waits a few frames before closing. The tooltip drops out for a frame here and there while the
    /// game rebuilds it, and closing on every one of those would mean reopening just as often -
    /// enough churn that the panels never finish opening at all.
    /// </summary>
    private void Idle()
    {
        if (idleFrames > IdleFramesBeforeClose)
            return;

        idleFrames++;
        if (idleFrames > IdleFramesBeforeClose)
            Hide();
    }

    /// <summary>
    /// To the right of the tooltip, flipping to the left when there is no room for it there.
    /// </summary>
    private static Vector2 Beside(AtkUnitBase* tooltip, float gap)
    {
        var width = NamePanelWindow.ExpectedWidth;
        var tooltipWidth = tooltip->RootNode->Width * tooltip->Scale;
        var right = tooltip->X + tooltipWidth + gap;

        // The game's own back buffer, not ImGui's viewport - this runs on the framework tick,
        // where there is no ImGui context to ask.
        var screenWidth = Device.Instance()->Width;
        var top = tooltip->Y + VerticalOffset;

        if (right + width > screenWidth)
            return new Vector2(Math.Max(0f, tooltip->X - width - gap), top);

        return new Vector2(right, top);
    }

    private void Hide() => window.IsOpen = false;

    private List<NameSection> Compose()
    {
        var sections = new List<NameSection>();

        if (!config.DecorateTooltip)
            return sections;

        var itemId = (uint)Plugin.GameGui.HoveredItem;
        if (itemId == 0)
            return sections;

        var client = names.GetItem(NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage), itemId);
        if (client.Name is null)
            return sections;

        var cycle = Cycle();
        if (cycle.Count == 0)
            return sections;

        if (config.CycleLanguagesWithScroll)
        {
            // A language can leave the cycle while it is the one being shown.
            if (selected is null || !cycle.Contains(selected.Value))
                selected = cycle[0];

            Append(sections, itemId, selected.Value, client);
            return sections;
        }

        foreach (var language in cycle)
            Append(sections, itemId, language, client);

        return sections;
    }

    /// <summary>Adds a panel's worth of text for one language, unless it has nothing to add.</summary>
    private void Append(List<NameSection> sections, uint itemId, GameLanguage language, ItemNames client)
    {
        var lines = ComposeOne(itemId, language, client);
        if (lines.Length > 0)
            sections.Add(new NameSection(language.ToString(), lines));
    }

    /// <summary>One language's text for each block that is switched on.</summary>
    private NameLine[] ComposeOne(uint itemId, GameLanguage language, ItemNames client)
    {
        var other = names.GetItem(language, itemId);
        var block = new List<NameLine>();

        Add(block, "Name", config.ShowItemName, other.Name, client.Name);
        Add(block, "Category", config.ShowItemCategory, other.Category, client.Category);
        Add(block, "Description", config.ShowItemDescription, other.Description, client.Description);

        return block.ToArray();
    }

    private void Add(List<NameLine> block, string tag, bool wanted, string? value, string? clientValue)
    {
        var text = value?.Trim();
        if (!wanted || string.IsNullOrEmpty(text))
            return;

        if (config.HideDuplicates && string.Equals(text, clientValue?.Trim(), StringComparison.Ordinal))
            return;

        block.Add(new NameLine(tag, text));
    }

}
