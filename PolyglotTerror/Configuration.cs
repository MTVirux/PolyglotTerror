using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;
using PolyglotTerror.Core;

namespace PolyglotTerror;

[Serializable]
public class Configuration : IPluginConfiguration
{
    private const int CurrentVersion = 2;

    public const int MinCastBarTextOffset = -40;

    public const int MaxCastBarTextOffset = 40;

    public const int MinTooltipPanelGap = 0;

    public const int MaxTooltipPanelGap = 40;

    public int Version { get; set; } = CurrentVersion;

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<LanguageEntry> Languages { get; set; } =
    [
        new(GameLanguage.English, true),
        new(GameLanguage.Japanese, true),
        new(GameLanguage.German, false),
        new(GameLanguage.French, false),
    ];

    public bool ShowItemName { get; set; } = true;

    public bool ShowItemCategory { get; set; }

    public bool ShowItemDescription { get; set; }

    public bool ShowActionName { get; set; } = true;

    public bool ShowActionDescription { get; set; }

    public bool DecorateTooltip { get; set; } = true;

    public bool DecorateActionTooltip { get; set; } = true;

    public bool DecorateOwnCastBar { get; set; } = true;

    public bool DecorateTargetBars { get; set; } = true;

    public bool DecorateOverheadBars { get; set; } = true;

    public bool DecoratePartyList { get; set; } = true;

    public bool HideDuplicates { get; set; } = true;

    /// <summary>
    /// Shows one language at a time in the panel, stepped through with the scroll wheel while an
    /// item is hovered, rather than stacking every enabled language at once.
    /// </summary>
    public bool CycleLanguagesWithScroll { get; set; } = true;

    /// <summary>
    /// Moves the cast bar text up or down without resizing the bar itself.
    /// </summary>
    public int CastBarTextOffset { get; set; }

    /// <summary>
    /// Distance between a tooltip and the panel of translated names beside it.
    /// </summary>
    public int TooltipPanelGap { get; set; } = 8;

    /// <summary>
    /// Repairs a config whose language list is missing entries, has duplicates, or
    /// carries values no longer in the enum, so a stale config cannot silently drop a language.
    /// </summary>
    public void Migrate()
    {
        var repaired = new List<LanguageEntry>();
        var seen = new HashSet<GameLanguage>();

        foreach (var entry in Languages)
        {
            if (!Enum.IsDefined(entry.Language) || !seen.Add(entry.Language))
                continue;

            repaired.Add(entry);
        }

        foreach (var language in Enum.GetValues<GameLanguage>())
        {
            if (seen.Add(language))
                repaired.Add(new LanguageEntry(language, false));
        }

        if (repaired.Count == Languages.Count && repaired.TrueForAll(Languages.Contains))
            return;

        Languages = repaired;
        Save();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
