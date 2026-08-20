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

namespace PolyglotTerror.Game;

/// <summary>
/// Keeps a column of <see cref="NamePanel"/> beside the item tooltip, one per language, filled with
/// the hovered item's names.
/// </summary>
/// <remarks>
/// The tooltip's own nodes are never written to. Content is picked up when the game refreshes the
/// tooltip; the panels are placed and closed from the framework tick, because a tooltip that is
/// merely hidden stops drawing without telling anyone.
/// </remarks>
public sealed unsafe class ItemTooltipNames : IDisposable
{
    private const string AddonName = "ItemDetail";
    private const int IdleFramesBeforeClose = 10;

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly TooltipForensics forensics;
    private readonly AddonInspector inspector;
    private readonly List<NamePanel> panels = [];

    private List<Section> sections = [];
    private GameLanguage? selected;
    private string state = string.Empty;
    private int idleFrames;
    private bool dumpArmed;
    private bool enabled;
    private bool disposed;
    private bool altWasHeld;

    public ItemTooltipNames(Configuration config, NameCatalog names, TooltipForensics forensics, AddonInspector inspector)
    {
        this.config = config;
        this.names = names;
        this.forensics = forensics;
        this.inspector = inspector;

        // One per language, made up front so the column never allocates an addon mid-hover.
        for (var i = 0; i < Enum.GetValues<GameLanguage>().Length; i++)
            panels.Add(new NamePanel(i));
    }

    public bool Enabled => enabled;

    /// <summary>
    /// Dumps the next tooltip's nodes. Typing a command dismisses the tooltip, so it has to be armed
    /// first and fire on the next hover.
    /// </summary>
    public void ArmDump() => dumpArmed = true;

    /// <summary>
    /// Must be called on the framework thread - KamiToolKit asserts on it whenever it opens or
    /// closes an addon.
    /// </summary>
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

        foreach (var panel in panels)
            panel.Dispose();
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
        forensics.Write($"name panels: {sections.Count}");

        for (var i = 0; i < sections.Count; i++)
            panels[i].SetContent(sections[i].Tag, sections[i].Lines);
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
    /// Records the tooltip's own visibility fields as Alt goes down and up, so the key check can be
    /// swapped for whichever of them the game actually moves.
    /// </summary>
    private void LogAltTransition(AtkUnitBase* tooltip, bool held)
    {
        if (held == altWasHeld)
            return;

        altWasHeld = held;

        if (tooltip == null)
        {
            forensics.Write($"alt {(held ? "down" : "up")}: no tooltip");
            return;
        }

        var root = tooltip->RootNode;
        forensics.Write(
            $"alt {(held ? "down" : "up")}: visible={tooltip->IsVisible} alpha={tooltip->Alpha} " +
            $"visFlags={tooltip->VisibilityFlags} showHide={tooltip->ShowHideFlags} " +
            $"rootVisible={(root != null && root->IsVisible())} rootAlpha={(root == null ? -1 : root->Color.A)}");
    }

    /// <summary>
    /// Follows the tooltip. It moves with the cursor every frame and is hidden rather than closed
    /// between hovers, so neither its position nor its visibility can be learnt from an event.
    /// </summary>
    private void OnFrameworkUpdate(IFramework framework)
    {
        var tooltip = Tooltip();
        var held = AltHeld;
        LogAltTransition(tooltip, held);

        if (held || !Showing(tooltip))
        {
            Note(held ? "idle: alt" : "idle: tooltip not showing");
            Idle();
            return;
        }

        if (config.CycleLanguagesWithScroll)
            StepOnScroll();

        if (sections.Count == 0)
        {
            Note("idle: nothing to show");
            Idle();
            return;
        }

        idleFrames = 0;
        Note("laying out");
        Lay(tooltip);
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

    /// <summary>Logs what the tick decided, but only when that changes.</summary>
    private void Note(string next)
    {
        if (state == next)
            return;

        state = next;
        forensics.Write($"panels: {next}");
    }

    /// <summary>
    /// Stacks the panels top to bottom beside the tooltip, lifting the column when it would run off
    /// the bottom of the screen.
    /// </summary>
    private void Lay(AtkUnitBase* tooltip)
    {
        var gap = config.TooltipPanelGap;
        var sizes = new Vector2[sections.Count];
        var total = 0f;

        for (var i = 0; i < sections.Count; i++)
        {
            sizes[i] = NamePanel.SizeFor(sections[i].Tag, sections[i].Lines);
            total += sizes[i].Y + (i > 0 ? gap : 0f);
        }

        var widest = 0f;
        foreach (var size in sizes)
            widest = Math.Max(widest, size.X);

        var x = ColumnX(tooltip, widest, gap);
        var y = (float)tooltip->Y;

        var screenHeight = Device.Instance()->Height;
        if (y + total > screenHeight)
            y = Math.Max(0f, screenHeight - total);

        for (var i = 0; i < panels.Count; i++)
        {
            var panel = panels[i];

            if (i >= sections.Count)
            {
                if (panel.IsOpen)
                    panel.Close();

                continue;
            }

            if (!panel.IsOpen)
            {
                panel.Size = sizes[i];
                panel.Open();
                forensics.Write(
                    $"panels: opened {i} isOpen={panel.IsOpen} at {x:F0},{y:F0} " +
                    $"size {sizes[i].X:F0}x{sizes[i].Y:F0}");
            }

            panel.SetWindowSize(sizes[i]);
            panel.SetWindowPosition(new Vector2(x, y));
            panel.SuppressInput();

            y += sizes[i].Y + gap;
        }
    }

    /// <summary>
    /// The column sits to the right of the tooltip, flipping to the left when there is no room.
    /// </summary>
    private static float ColumnX(AtkUnitBase* tooltip, float width, float gap)
    {
        var tooltipWidth = tooltip->RootNode->Width * tooltip->Scale;
        var right = tooltip->X + tooltipWidth + gap;

        // The game's own back buffer, not ImGui's viewport - this runs on the framework tick,
        // where there is no ImGui context to ask.
        var screenWidth = Device.Instance()->Width;
        if (right + width > screenWidth)
            return Math.Max(0f, tooltip->X - width - gap);

        return right;
    }

    /// <summary>
    /// Closes the panels rather than just hiding them. An addon that is merely invisible is still in
    /// the game's hit testing, and these open above the interface, so leaving them around swallows
    /// mouseovers for everything beneath.
    /// </summary>
    private void Hide()
    {
        foreach (var panel in panels)
        {
            if (panel.IsOpen)
                panel.Close();
        }
    }

    private List<Section> Compose()
    {
        var sections = new List<Section>();

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
    private void Append(List<Section> sections, uint itemId, GameLanguage language, ItemNames client)
    {
        var lines = ComposeOne(itemId, language, client);
        if (lines.Length > 0)
            sections.Add(new Section(language.ToString(), lines));
    }

    /// <summary>One language's text for each block that is switched on.</summary>
    private string[] ComposeOne(uint itemId, GameLanguage language, ItemNames client)
    {
        var other = names.GetItem(language, itemId);
        var block = new List<string>();

        Add(block, config.ShowItemName, other.Name, client.Name);
        Add(block, config.ShowItemCategory, other.Category, client.Category);
        Add(block, config.ShowItemDescription, other.Description, client.Description);

        return block.ToArray();
    }

    private void Add(List<string> block, bool wanted, string? value, string? clientValue)
    {
        var text = value?.Trim();
        if (!wanted || string.IsNullOrEmpty(text))
            return;

        if (config.HideDuplicates && string.Equals(text, clientValue?.Trim(), StringComparison.Ordinal))
            return;

        if (block.Count > 0)
            block.Add(string.Empty);

        block.Add(text);
    }

    private readonly record struct Section(string Tag, string[] Lines);
}
