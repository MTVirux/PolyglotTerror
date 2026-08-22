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
/// Keeps a <see cref="NamePanelWindow"/> beside a game tooltip and filled with the hovered
/// subject's names, one block per language.
/// </summary>
/// <remarks>
/// The tooltip's own nodes are never touched. Content is picked up when the game refreshes the
/// tooltip; the panel is placed and hidden from the framework tick, because a tooltip that is merely
/// hidden stops drawing without telling anyone.
/// </remarks>
public abstract unsafe class TooltipNamePanel : IDisposable
{
    private const int IdleFramesBeforeClose = 10;

    private readonly string addonName;
    private readonly NamePanelWindow window;

    private List<NameSection> sections = [];
    private GameLanguage? selected;
    private int idleFrames;
    private bool enabled;
    private bool disposed;

    protected TooltipNamePanel(
        Configuration config,
        NameCatalog names,
        NamePanelWindow window,
        string addonName)
    {
        Config = config;
        Names = names;
        this.window = window;
        this.addonName = addonName;
    }

    public bool Enabled => enabled;

    protected Configuration Config { get; }

    protected NameCatalog Names { get; }

    /// <summary>Whether the user wants this panel at all.</summary>
    protected abstract bool Wanted { get; }

    /// <summary>Show one language at a time, stepped with the scroll wheel, rather than all of them.</summary>
    protected abstract bool CycleWithScroll { get; }

    /// <summary>Distance between the tooltip and the panel.</summary>
    protected abstract int Gap { get; }

    /// <summary>How far the panel sits below the top of the tooltip.</summary>
    protected abstract int OffsetY { get; }

    /// <summary>The client's own language, which the tooltip is already showing.</summary>
    protected static GameLanguage ClientLanguage
        => NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage);

    public void SetEnabled(bool value)
    {
        if (disposed || value == enabled)
            return;

        enabled = value;

        if (value)
        {
            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, addonName, OnRequestedUpdate);
            Plugin.Framework.Update += OnFrameworkUpdate;
        }
        else
        {
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, addonName, OnRequestedUpdate);
            Plugin.Framework.Update -= OnFrameworkUpdate;
            Hide();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        SetEnabled(false);
        disposed = true;
        Hide();
    }

    /// <summary>
    /// Reads whatever is hovered right now, returning false when there is nothing to translate.
    /// Called before any language block is composed.
    /// </summary>
    protected abstract bool TryResolveSubject();

    /// <summary>One language's text for the subject resolved by <see cref="TryResolveSubject"/>.</summary>
    protected abstract NameLine[] ComposeOne(GameLanguage language);

    /// <summary>Runs after the game has refreshed the tooltip, once the panel has been rebuilt.</summary>
    protected virtual void OnUpdated(AddonArgs args)
    {
    }

    /// <summary>What the game itself calls this field, in the language the client is running in.</summary>
    protected string Tag(uint labelRow) => Names.GetUiText(ClientLanguage, labelRow) ?? string.Empty;

    /// <summary>Adds one line, unless it is switched off, empty, or a repeat of the client's own text.</summary>
    protected void Add(List<NameLine> block, uint labelRow, bool wanted, string? value, string? clientValue)
    {
        var text = value?.Trim();
        if (!wanted || string.IsNullOrEmpty(text))
            return;

        if (Config.HideDuplicates && string.Equals(text, clientValue?.Trim(), StringComparison.Ordinal))
            return;

        block.Add(new NameLine(Tag(labelRow), text));
    }

    private static bool Showing(AtkUnitBase* tooltip)
        => tooltip != null
           && tooltip->IsVisible
           && tooltip->RootNode != null
           && tooltip->RootNode->IsVisible()
           && tooltip->RootNode->Color.A > 0;

    /// <summary>
    /// Holding Alt hides the tooltip, but none of the addon's own visibility fields move when it
    /// does, so the key is read directly rather than inferred from the tooltip.
    /// </summary>
    private static bool AltHeld
        => Plugin.KeyState[VirtualKey.MENU]
           || Plugin.KeyState[VirtualKey.LMENU]
           || Plugin.KeyState[VirtualKey.RMENU];

    /// <summary>
    /// To the right of the tooltip, flipping to the left when there is no room for it there.
    /// </summary>
    private static Vector2 Beside(AtkUnitBase* tooltip, float gap, float offsetY)
    {
        var width = NamePanelWindow.ExpectedWidth;
        var tooltipWidth = tooltip->RootNode->Width * tooltip->Scale;
        var right = tooltip->X + tooltipWidth + gap;

        // The game's own back buffer, not ImGui's viewport - this runs on the framework tick,
        // where there is no ImGui context to ask.
        var screenWidth = Device.Instance()->Width;
        var top = tooltip->Y + offsetY;

        if (right + width > screenWidth)
            return new Vector2(Math.Max(0f, tooltip->X - width - gap), top);

        return new Vector2(right, top);
    }

    private AtkUnitBase* Tooltip() => (AtkUnitBase*)Plugin.GameGui.GetAddonByName(addonName).Address;

    private void OnRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        Rebuild();
        OnUpdated(args);
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
        var client = ClientLanguage;
        var cycle = new List<GameLanguage>();

        foreach (var entry in Config.Languages)
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

        if (CycleWithScroll)
            StepOnScroll();

        if (sections.Count == 0)
        {
            Idle();
            return;
        }

        idleFrames = 0;
        window.Anchor = Beside(tooltip, Gap, OffsetY);
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

    private void Hide() => window.IsOpen = false;

    private List<NameSection> Compose()
    {
        var composed = new List<NameSection>();

        if (!Wanted || !TryResolveSubject())
            return composed;

        var cycle = Cycle();
        if (cycle.Count == 0)
            return composed;

        if (CycleWithScroll)
        {
            // A language can leave the cycle while it is the one being shown.
            if (selected is null || !cycle.Contains(selected.Value))
                selected = cycle[0];

            Append(composed, selected.Value);
            return composed;
        }

        foreach (var language in cycle)
            Append(composed, language);

        return composed;
    }

    /// <summary>Adds a panel's worth of text for one language, unless it has nothing to add.</summary>
    private void Append(List<NameSection> composed, GameLanguage language)
    {
        var lines = ComposeOne(language);
        if (lines.Length > 0)
            composed.Add(new NameSection(language.ToString(), lines));
    }
}
