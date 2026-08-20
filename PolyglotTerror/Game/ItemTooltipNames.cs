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
/// Keeps <see cref="NamePanel"/> beside the item tooltip and filled with the hovered item's names.
/// </summary>
/// <remarks>
/// The tooltip's own nodes are never written to. Content is picked up when the game refreshes the
/// tooltip; the panel is placed and hidden from the framework tick, because a tooltip that is merely
/// hidden stops drawing without telling anyone.
/// </remarks>
public sealed unsafe class ItemTooltipNames : IDisposable
{
    private const string AddonName = "ItemDetail";

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly TooltipForensics forensics;
    private readonly NamePanel panel = new();

    private string[] lines = [];
    private string? tag;
    private GameLanguage? selected;
    private bool enabled;
    private bool disposed;
    private bool altWasHeld;

    public ItemTooltipNames(Configuration config, NameCatalog names, TooltipForensics forensics)
    {
        this.config = config;
        this.names = names;
        this.forensics = forensics;
    }

    public bool Enabled => enabled;

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
            CloseNow();
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
            CloseNow();
        }

        panel.Dispose();
    }

    private static AtkUnitBase* Tooltip()
        => (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName).Address;

    private void OnRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        (tag, lines) = Compose();
        forensics.Write($"name panel: {lines.Length} lines tag={tag ?? "all"}");

        if (lines.Length > 0)
            panel.SetContent(tag, lines);
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
            Hide();
            return;
        }

        if (config.CycleLanguagesWithScroll)
            StepOnScroll();

        if (lines.Length == 0)
        {
            Hide();
            return;
        }

        var scale = tooltip->Scale;
        var size = NamePanel.SizeFor(tag, lines);

        if (!panel.IsOpen)
        {
            panel.Size = size;
            panel.Open();
        }

        panel.SetWindowSize(size);
        panel.SetWindowPosition(Beside(tooltip, scale, size, config.TooltipPanelGap));
        panel.SuppressInput();
        panel.SetVisible(true);
    }

    /// <summary>
    /// To the right of the tooltip, flipping to the left when there is no room for it there.
    /// </summary>
    private static Vector2 Beside(AtkUnitBase* tooltip, float scale, Vector2 size, float gap)
    {
        var width = tooltip->RootNode->Width * scale;
        var right = tooltip->X + width + gap;

        // The game's own back buffer, not ImGui's viewport - this runs on the framework tick,
        // where there is no ImGui context to ask.
        var screenWidth = Device.Instance()->Width;
        if (right + size.X > screenWidth)
            return new Vector2(Math.Max(0f, tooltip->X - size.X - gap), tooltip->Y);

        return new Vector2(right, tooltip->Y);
    }

    private void Hide() => panel.SetVisible(false);

    private void CloseNow()
    {
        if (panel.IsOpen)
            panel.Close();
    }

    private (string? Tag, string[] Lines) Compose()
    {
        if (!config.DecorateTooltip)
            return (null, []);

        var itemId = (uint)Plugin.GameGui.HoveredItem;
        if (itemId == 0)
            return (null, []);

        var client = names.GetItem(NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage), itemId);
        if (client.Name is null)
            return (null, []);

        if (!config.CycleLanguagesWithScroll)
            return (null, ComposeAll(itemId, client));

        var cycle = Cycle();
        if (cycle.Count == 0)
            return (null, []);

        // A language can leave the cycle while it is the one being shown.
        if (selected is null || !cycle.Contains(selected.Value))
            selected = cycle[0];

        return (selected.Value.ToString(), ComposeOne(itemId, selected.Value, client));
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

    private string[] ComposeAll(uint itemId, ItemNames client)
    {
        var block = new List<string>();

        if (config.ShowItemName && client.Name is not null)
            AddSection(block, itemId, client.Name, static item => item.Name);

        if (config.ShowItemCategory && client.Category is not null)
            AddSection(block, itemId, client.Category, static item => item.Category);

        if (config.ShowItemDescription && client.Description is not null)
            AddSection(block, itemId, client.Description, static item => item.Description);

        return block.ToArray();
    }


    /// <summary>
    /// Adds one block's translations, separated from the block before it by a blank line. The first
    /// line composed is the text the game already drew, so it is dropped.
    /// </summary>
    private void AddSection(List<string> block, uint itemId, string clientText, Func<ItemNames, string?> pick)
    {
        var candidates = new Dictionary<GameLanguage, string?>();
        foreach (var entry in config.Languages)
        {
            if (entry.Enabled)
                candidates[entry.Language] = pick(names.GetItem(entry.Language, itemId));
        }

        var composed = LineComposer.BuildLines(clientText, candidates, config.Languages, config.HideDuplicates);
        if (composed.Count <= 1)
            return;

        if (block.Count > 0)
            block.Add(string.Empty);

        for (var i = 1; i < composed.Count; i++)
            block.Add(composed[i]);
    }
}
