using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;
using PolyglotTerror.Core;

namespace PolyglotTerror;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public const int MinCastBarTextOffset = -40;

    public const int MaxCastBarTextOffset = 40;

    public const int MinTooltipNameSpace = 0;

    public const int MaxTooltipNameSpace = 40;

    public int Version { get; set; } = 1;

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
    /// Moves the cast bar text up or down without resizing the bar itself.
    /// </summary>
    public int CastBarTextOffset { get; set; }

    /// <summary>
    /// Resizes the tooltip so extra name lines fit. Off by default: the game gives the name a fixed
    /// two-line region, so making room means moving the blocks below it and growing the window.
    /// </summary>
    public bool ExpandTooltipName { get; set; }

    /// <summary>
    /// Extra room added under the last line of a tooltip's name block.
    /// </summary>
    public int TooltipNameExtraSpace { get; set; }

    /// <summary>
    /// Pushes a tooltip's name block down, growing the header by the same amount.
    /// </summary>
    public int TooltipNameTopOffset { get; set; }

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
