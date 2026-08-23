using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PolyglotTerror.Core;

namespace PolyglotTerror.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly Vector4 Amber = new(1f, 0.8f, 0f, 1f);

    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base(
        "PolyglotTerror Settings###PolyglotTerrorConfig",
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        configuration = plugin.Configuration;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        DrawLanguages();
        DrawItems();
        DrawActions();
        DrawSurfaces();
        DrawDisplay();

        if (LineComposer.Primary(configuration.Languages) is null)
        {
            ImGui.Spacing();
            ImGui.TextColored(Amber, "No language is enabled, so nothing will be shown.");
        }
    }

    private void DrawLanguages()
    {
        if (!Section("Languages"))
            return;

        var languages = configuration.Languages;
        for (var i = 0; i < languages.Count; i++)
        {
            var entry = languages[i];

            var enabled = entry.Enabled;
            if (ImGui.Checkbox(entry.Language.ToString(), ref enabled))
            {
                languages[i] = entry with { Enabled = enabled };
                Apply();
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(i == 0);
            if (ImGui.ArrowButton($"##up{i}", ImGuiDir.Up))
                Swap(i, i - 1);
            ImGui.EndDisabled();

            ImGui.SameLine();
            ImGui.BeginDisabled(i == languages.Count - 1);
            if (ImGui.ArrowButton($"##down{i}", ImGuiDir.Down))
                Swap(i, i + 1);
            ImGui.EndDisabled();

            if (i == 0)
            {
                ImGui.SameLine();
                HelpMarker("Lines are shown in this order. The first enabled language is the primary one.");
            }
        }
    }

    private void DrawItems()
    {
        if (!Section("Items"))
            return;

        // Both sections label their checkboxes the same way, so they need separate ID scopes.
        ImGui.PushID("items");

        Option("Name", configuration.ShowItemName, value => configuration.ShowItemName = value);
        Option("Category", configuration.ShowItemCategory, value => configuration.ShowItemCategory = value);
        Option(
            "Description",
            configuration.ShowItemDescription,
            value => configuration.ShowItemDescription = value,
            "Descriptions in four languages make for a very tall panel.");

        DrawPanelPlacement(
            "Item translations appear beside the tooltip, and hide with it.",
            configuration.CycleLanguagesWithScroll,
            value => configuration.CycleLanguagesWithScroll = value,
            configuration.TooltipPanelGap,
            value => configuration.TooltipPanelGap = value,
            configuration.TooltipPanelOffsetY,
            value => configuration.TooltipPanelOffsetY = value);

        ImGui.PopID();
    }

    private void DrawActions()
    {
        if (!Section("Actions"))
            return;

        ImGui.PushID("actions");

        Option("Name", configuration.ShowActionName, value => configuration.ShowActionName = value);
        Option("Description", configuration.ShowActionDescription, value => configuration.ShowActionDescription = value);

        DrawPanelPlacement(
            "Action translations appear beside the tooltip, and hide with it.",
            configuration.CycleActionLanguagesWithScroll,
            value => configuration.CycleActionLanguagesWithScroll = value,
            configuration.ActionPanelGap,
            value => configuration.ActionPanelGap = value,
            configuration.ActionPanelOffsetY,
            value => configuration.ActionPanelOffsetY = value);

        ImGui.PopID();
    }

    /// <summary>The knobs both name panels have, drawn against whichever one's settings are passed in.</summary>
    private void DrawPanelPlacement(
        string blurb,
        bool cycle,
        Action<bool> setCycle,
        int gap,
        Action<int> setGap,
        int offsetY,
        Action<int> setOffsetY)
    {
        ImGui.Spacing();
        Option(
            "Show one language at a time, scroll to change it",
            cycle,
            setCycle,
            "Off shows every language at once, one panel each, stacked top to bottom.\n\n" +
            "Scrolling still scrolls whatever is under the cursor as well.");

        Slider(
            "Gap beside the tooltip",
            gap,
            Configuration.MinTooltipPanelGap,
            Configuration.MaxTooltipPanelGap,
            setGap,
            blurb);

        Slider(
            "Vertical offset",
            offsetY,
            Configuration.MinTooltipPanelOffsetY,
            Configuration.MaxTooltipPanelOffsetY,
            setOffsetY,
            "Moves the panel up or down relative to the top of the tooltip.");
    }

    private void DrawSurfaces()
    {
        if (!Section("Where to show translations"))
            return;

        Option(
            "Item tooltips",
            configuration.DecorateTooltip,
            value => configuration.DecorateTooltip = value,
            "Changes in this section apply after the plugin reloads.");
        Option("Action tooltips", configuration.DecorateActionTooltip, value => configuration.DecorateActionTooltip = value);
        Option("Your own cast bar", configuration.DecorateOwnCastBar, value => configuration.DecorateOwnCastBar = value);
        Option("Target and focus target cast bars", configuration.DecorateTargetBars, value => configuration.DecorateTargetBars = value);
        Option("Cast bars over enemies", configuration.DecorateOverheadBars, value => configuration.DecorateOverheadBars = value);
        Option("Party list (primary language only)", configuration.DecoratePartyList, value => configuration.DecoratePartyList = value);
    }

    private void DrawDisplay()
    {
        if (!Section("Display"))
            return;

        Option(
            "Hide a language when its name is identical to one already shown",
            configuration.HideDuplicates,
            value => configuration.HideDuplicates = value);

        Slider(
            "Cast bar text offset",
            configuration.CastBarTextOffset,
            Configuration.MinCastBarTextOffset,
            Configuration.MaxCastBarTextOffset,
            value => configuration.CastBarTextOffset = value,
            "Moves the cast bar text up or down. The bar itself stays where it is.");
    }

    private void Swap(int a, int b)
    {
        (configuration.Languages[a], configuration.Languages[b]) =
            (configuration.Languages[b], configuration.Languages[a]);
        Apply();
    }

    private void Option(string label, bool value, Action<bool> set, string? help = null)
    {
        if (ImGui.Checkbox(label, ref value))
        {
            set(value);
            Apply();
        }

        Help(help);
    }

    private void Slider(string label, int value, int min, int max, Action<int> set, string? help = null)
    {
        if (ImGui.SliderInt(label, ref value, min, max))
        {
            set(value);
            Apply();
        }

        Help(help);
    }

    private void Apply()
    {
        configuration.Save();
        plugin.Names.Clear();
    }

    private static bool Section(string label)
    {
        ImGui.Spacing();
        if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
            return false;

        ImGui.Spacing();
        return true;
    }

    private static void Help(string? text)
    {
        if (text is null)
            return;

        ImGui.SameLine();
        HelpMarker(text);
    }

    private static void HelpMarker(string text)
    {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
