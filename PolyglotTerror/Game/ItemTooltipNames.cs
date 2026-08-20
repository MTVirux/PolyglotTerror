using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
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
        lines = ComposeLines();
        forensics.Write($"name panel: {lines.Length} lines");

        if (lines.Length > 0)
            panel.SetLines(lines);
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

        if (held || !Showing(tooltip) || lines.Length == 0)
        {
            Hide();
            return;
        }

        var scale = tooltip->Scale;
        var size = NamePanel.SizeFor(lines);

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

    private string[] ComposeLines()
    {
        if (!config.DecorateTooltip)
            return [];

        var itemId = (uint)Plugin.GameGui.HoveredItem;
        if (itemId == 0)
            return [];

        var client = names.GetItem(NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage), itemId);
        if (client.Name is null)
            return [];

        var block = new List<string>();

        if (config.ShowItemName)
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
