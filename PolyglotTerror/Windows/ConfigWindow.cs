using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PolyglotTerror.Core;

namespace PolyglotTerror.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
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
    }

    private void DrawLanguages()
    {
        if (!Section("Languages"))
            return;

        var client = configuration.ClientLanguage;

        ImGui.TextDisabled($"Saved separately for each game language. The game is in {client}.");
        ImGui.SameLine();
        HelpMarker(
            "Lines are shown in this order. The first enabled language is the primary one." +
            "\n\n" +
            "Every game language keeps its own set, so switching the client to another language " +
            "brings up the set you picked for that one.");
        ImGui.Spacing();

        var languages = configuration.Languages;
        for (var i = 0; i < languages.Count; i++)
        {
            var entry = languages[i];
            var locked = entry.Language == client;

            var enabled = entry.Enabled;
            ImGui.BeginDisabled(locked);
            if (ImGui.Checkbox(entry.Language.ToString(), ref enabled))
            {
                languages[i] = entry with { Enabled = enabled };
                Apply();
            }

            ImGui.EndDisabled();

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

            if (!locked)
                continue;

            ImGui.SameLine();
            HelpMarker(
                "The game is running in this language, so it is what every other line is a " +
                "translation of. It stays on, and turns back on by itself whenever you start the " +
                "game in a different language.");
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
        Option("Category", configuration.ShowActionCategory, value => configuration.ShowActionCategory = value);
        Option("Description", configuration.ShowActionDescription, value => configuration.ShowActionDescription = value);

        DrawPanelPlacement(
            "Action translations appear beside the tooltip, and hide with it.",
            configuration.ActionPanelGap,
            value => configuration.ActionPanelGap = value,
            configuration.ActionPanelOffsetY,
            value => configuration.ActionPanelOffsetY = value);

        ImGui.PopID();
    }

    /// <summary>The knobs both name panels have, drawn against whichever one's settings are passed in.</summary>
    private void DrawPanelPlacement(
        string blurb,
        int gap,
        Action<int> setGap,
        int offsetY,
        Action<int> setOffsetY)
    {
        ImGui.Spacing();
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
        Option("Party list", configuration.DecoratePartyList, value => configuration.DecoratePartyList = value);
        DrawPartyListLanguage();
    }

    /// <summary>
    /// The party list has room for one line per member, so it picks a single language of its own
    /// instead of following the list above.
    /// </summary>
    private void DrawPartyListLanguage()
    {
        ImGui.BeginDisabled(!configuration.DecoratePartyList);

        var current = configuration.PartyListLanguage;
        if (ImGui.BeginCombo("Party list language", current.ToString()))
        {
            foreach (var language in Enum.GetValues<GameLanguage>())
            {
                if (!ImGui.Selectable(language.ToString(), language == current))
                    continue;

                configuration.PartyListLanguage = language;
                Apply();
            }

            ImGui.EndCombo();
        }

        ImGui.EndDisabled();
        Help(
            "The party list shows this one language instead of the whole list, and starts on the " +
            "language the game is in. Saved separately for each game language.");
    }

    private void DrawDisplay()
    {
        if (!Section("Display"))
            return;

        // Two of these read the same as the checkboxes above, so they need their own ID scope.
        ImGui.PushID("offsets");

        ImGui.TextDisabled("Moves each cast bar's text up or down. The bars themselves stay where they are.");
        ImGui.Spacing();

        OffsetSlider(
            "Your own cast bar",
            configuration.PlayerCastBarTextOffset,
            value => configuration.PlayerCastBarTextOffset = value);
        OffsetSlider(
            "Target cast bar",
            configuration.TargetCastBarTextOffset,
            value => configuration.TargetCastBarTextOffset = value);
        OffsetSlider(
            "Cast bars over enemies",
            configuration.OverheadCastBarTextOffset,
            value => configuration.OverheadCastBarTextOffset = value);
        OffsetSlider(
            "Focus target cast bar",
            configuration.FocusTargetCastBarTextOffset,
            value => configuration.FocusTargetCastBarTextOffset = value);

        ImGui.PopID();
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

    private void OffsetSlider(string label, int value, Action<int> set) => Slider(
        label,
        value,
        Configuration.MinCastBarTextOffset,
        Configuration.MaxCastBarTextOffset,
        set);

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
