using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PolyglotTerror.Core;

namespace PolyglotTerror.Windows;

/// <summary>
/// The translated names, drawn beside the item tooltip.
/// </summary>
/// <remarks>
/// This was a native game window built with KamiToolKit, which meant fighting the game for hit
/// testing, draw order and the nine parts of a window frame - and losing. ImGui draws over the game
/// and answers to nobody, so none of that applies.
/// </remarks>
public sealed class NamePanelWindow : Window
{
    private const float Width = 300f;

    private static readonly Vector4 LanguageColour = new(0.65f, 0.62f, 0.55f, 1f);

    private IReadOnlyList<NameSection> sections = [];

    public NamePanelWindow() : base("PolyglotTerror Names###PolyglotNames")
    {
        // NoInputs is the important one: the panel sits next to the cursor, and anything that
        // answers the mouse takes the hover away from the item underneath it.
        Flags = ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.AlwaysAutoResize;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    /// <summary>Where the panel's top left corner goes, in screen pixels.</summary>
    public Vector2 Anchor { get; set; }

    /// <summary>The width the caller should assume when deciding which side of the tooltip to use.</summary>
    public static float ExpectedWidth => Width;

    public void SetSections(IReadOnlyList<NameSection> value) => sections = value;

    public override void PreDraw()
    {
        ImGui.SetNextWindowPos(Anchor, ImGuiCond.Always);
        ImGui.SetNextWindowSizeConstraints(new Vector2(Width, 0f), new Vector2(Width, float.MaxValue));
    }

    public override void Draw()
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (i > 0)
                ImGui.Separator();

            var section = sections[i];
            ImGui.TextColored(LanguageColour, section.Language);

            foreach (var line in section.Lines)
            {
                if (line.Length == 0)
                    ImGui.Spacing();
                else
                    ImGui.TextWrapped(line);
            }
        }
    }
}
