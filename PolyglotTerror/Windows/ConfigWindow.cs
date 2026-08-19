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
        DrawTooltips();
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
        Heading("Languages");

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
        }

        ImGui.TextDisabled("Lines are shown in this order. The first enabled language is the primary one.");
    }

    private void DrawTooltips()
    {
        Heading("Tooltips");

        Option("Item name", configuration.ShowItemName, value => configuration.ShowItemName = value);
        Option("Item category", configuration.ShowItemCategory, value => configuration.ShowItemCategory = value);
        Option("Item description", configuration.ShowItemDescription, value => configuration.ShowItemDescription = value);
        Option("Action name", configuration.ShowActionName, value => configuration.ShowActionName = value);
        Option("Action description", configuration.ShowActionDescription, value => configuration.ShowActionDescription = value);

        ImGui.TextDisabled("Descriptions in four languages make for a very tall tooltip.");
    }

    private void DrawSurfaces()
    {
        Heading("Where to show translations");

        Option("Item tooltips", configuration.DecorateTooltip, value => configuration.DecorateTooltip = value);
        Option("Action tooltips", configuration.DecorateActionTooltip, value => configuration.DecorateActionTooltip = value);
        Option("Your own cast bar", configuration.DecorateOwnCastBar, value => configuration.DecorateOwnCastBar = value);
        Option("Target and focus target cast bars", configuration.DecorateTargetBars, value => configuration.DecorateTargetBars = value);
        Option("Cast bars over enemies", configuration.DecorateOverheadBars, value => configuration.DecorateOverheadBars = value);
        Option("Party list (primary language only)", configuration.DecoratePartyList, value => configuration.DecoratePartyList = value);

        ImGui.TextDisabled("Changes here apply after the plugin reloads.");
    }

    private void DrawDisplay()
    {
        Heading("Display");

        Option(
            "Hide a language when its name is identical to one already shown",
            configuration.HideDuplicates,
            value => configuration.HideDuplicates = value);

        var offset = configuration.CastBarTextOffset;
        if (ImGui.SliderInt(
                "Cast bar text offset",
                ref offset,
                Configuration.MinCastBarTextOffset,
                Configuration.MaxCastBarTextOffset))
        {
            configuration.CastBarTextOffset = offset;
            Apply();
        }

        ImGui.TextDisabled("Moves the cast bar text up or down. The bar itself stays where it is.");

        Option(
            "Resize tooltips to fit extra name lines",
            configuration.ExpandTooltipName,
            value => configuration.ExpandTooltipName = value);

        ImGui.BeginDisabled(!configuration.ExpandTooltipName);

        var space = configuration.TooltipNameExtraSpace;
        if (ImGui.SliderInt(
                "Tooltip name space",
                ref space,
                Configuration.MinTooltipNameSpace,
                Configuration.MaxTooltipNameSpace))
        {
            configuration.TooltipNameExtraSpace = space;
            Apply();
        }

        var top = configuration.TooltipNameTopOffset;
        if (ImGui.SliderInt(
                "Tooltip name top offset",
                ref top,
                Configuration.MinTooltipNameSpace,
                Configuration.MaxTooltipNameSpace))
        {
            configuration.TooltipNameTopOffset = top;
            Apply();
        }

        ImGui.EndDisabled();

        ImGui.TextDisabled("Extra room under the tooltip name, and how far down it starts.");
    }

    private void Swap(int a, int b)
    {
        (configuration.Languages[a], configuration.Languages[b]) =
            (configuration.Languages[b], configuration.Languages[a]);
        Apply();
    }

    private void Option(string label, bool value, Action<bool> set)
    {
        if (!ImGui.Checkbox(label, ref value))
            return;

        set(value);
        Apply();
    }

    private void Apply()
    {
        configuration.Save();
        plugin.Names.Clear();
    }

    private static void Heading(string label)
    {
        ImGui.Spacing();
        ImGui.Text(label);
        ImGui.Separator();
        ImGui.Spacing();
    }
}
