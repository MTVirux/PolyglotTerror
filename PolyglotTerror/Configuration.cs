using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;
using PolyglotTerror.Core;

namespace PolyglotTerror;

[Serializable]
public class Configuration : IPluginConfiguration
{
    private const int CurrentVersion = 5;

    /// <summary>The version that first put each list's own language at the top of it.</summary>
    private const int PrimaryFirstVersion = 4;

    public const int MinCastBarTextOffset = -40;

    public const int MaxCastBarTextOffset = 40;

    public const int MinTooltipPanelGap = 0;

    public const int MaxTooltipPanelGap = 40;

    public const int MinTooltipPanelOffsetY = -100;

    public const int MaxTooltipPanelOffsetY = 100;

    /// <summary>The one offset of a config written before every cast bar had its own.</summary>
    [JsonProperty("CastBarTextOffset", NullValueHandling = NullValueHandling.Ignore)]
    private int? sharedCastBarTextOffset;

    /// <summary>The language list of a config written before the lists were kept per game language.</summary>
    [JsonProperty("Languages", NullValueHandling = NullValueHandling.Ignore, ObjectCreationHandling = ObjectCreationHandling.Replace)]
    private List<LanguageEntry>? sharedLanguages;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// One language list per game language, because a player who switches the client between
    /// languages wants a different set of translations in each.
    /// </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<GameLanguage, List<LanguageEntry>> LanguagesByClient { get; set; } = new();

    /// <summary>The language the game is running in, read once when the plugin starts.</summary>
    [JsonIgnore]
    public GameLanguage ClientLanguage { get; private set; } = GameLanguage.English;

    /// <summary>The language list belonging to the language the game is running in.</summary>
    [JsonIgnore]
    public List<LanguageEntry> Languages => LanguagesByClient[ClientLanguage];

    /// <summary>
    /// The party list has room for one line per member, so it shows a single language rather than
    /// the whole list. Kept per game language for the same reason the lists are.
    /// </summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<GameLanguage, GameLanguage> PartyListLanguageByClient { get; set; } = new();

    /// <summary>The language the party list shows while the game is in the language it is in.</summary>
    [JsonIgnore]
    public GameLanguage PartyListLanguage
    {
        get => PartyListLanguageByClient[ClientLanguage];
        set => PartyListLanguageByClient[ClientLanguage] = value;
    }

    public bool ShowItemName { get; set; } = true;

    public bool ShowItemCategory { get; set; }

    public bool ShowItemDescription { get; set; }

    public bool ShowActionName { get; set; } = true;

    public bool ShowActionCategory { get; set; }

    public bool ShowActionDescription { get; set; }

    public bool DecorateTooltip { get; set; } = true;

    public bool DecorateActionTooltip { get; set; } = true;

    public bool DecorateOwnCastBar { get; set; } = true;

    public bool DecorateTargetBars { get; set; } = true;

    public bool DecorateOverheadBars { get; set; } = true;

    public bool DecoratePartyList { get; set; } = true;

    /// <summary>Moves your own cast bar's text up or down without resizing the bar itself.</summary>
    public int PlayerCastBarTextOffset { get; set; }

    /// <summary>Moves the target's cast bar text up or down without resizing the bar itself.</summary>
    public int TargetCastBarTextOffset { get; set; }

    /// <summary>Moves the text of the cast bars over enemies' heads up or down.</summary>
    public int OverheadCastBarTextOffset { get; set; }

    /// <summary>Moves the focus target's cast bar text up or down without resizing the bar itself.</summary>
    public int FocusTargetCastBarTextOffset { get; set; }

    /// <summary>
    /// Distance between a tooltip and the panel of translated names beside it.
    /// </summary>
    public int TooltipPanelGap { get; set; } = 8;

    /// <summary>
    /// Moves the panel of translated names up or down relative to the top of the tooltip.
    /// </summary>
    public int TooltipPanelOffsetY { get; set; } = 20;

    /// <summary>Distance between an action tooltip and the panel of translated names beside it.</summary>
    public int ActionPanelGap { get; set; } = 8;

    /// <summary>Moves the action panel up or down relative to the top of the action tooltip.</summary>
    public int ActionPanelOffsetY { get; set; } = 20;

    /// <summary>
    /// Points the config at the language the game is running in and repairs that language's list,
    /// so a stale config cannot drop a language, carry a duplicate, or leave the game's own
    /// language switched off.
    /// </summary>
    public void Migrate(GameLanguage clientLanguage)
    {
        ClientLanguage = clientLanguage;

        var changed = Version != CurrentVersion;
        var promotePrimary = Version < PrimaryFirstVersion;
        Version = CurrentVersion;

        if (sharedCastBarTextOffset is { } inheritedOffset)
        {
            // The one offset the old config had applies to every cast bar until they are changed apart.
            PlayerCastBarTextOffset = inheritedOffset;
            TargetCastBarTextOffset = inheritedOffset;
            OverheadCastBarTextOffset = inheritedOffset;
            FocusTargetCastBarTextOffset = inheritedOffset;
            sharedCastBarTextOffset = null;
            changed = true;
        }

        if (sharedLanguages is { } inherited)
        {
            // The one list the old config had belongs to whichever language the game is in now.
            LanguagesByClient.TryAdd(clientLanguage, new List<LanguageEntry>(inherited));
            sharedLanguages = null;
            changed = true;
        }

        if (!LanguagesByClient.TryGetValue(clientLanguage, out var entries))
        {
            // A language the game has never run in starts with nothing but its own names.
            entries = [];
            LanguagesByClient[clientLanguage] = entries;
            changed = true;
        }

        if (promotePrimary)
        {
            // Lists written before this ran carry the order the languages happen to be declared in.
            foreach (var (language, list) in LanguagesByClient)
                MoveToFront(list, language);
        }

        // The party list starts out showing the language the game is in.
        if (!PartyListLanguageByClient.TryGetValue(clientLanguage, out var partyLanguage) || !Enum.IsDefined(partyLanguage))
        {
            PartyListLanguageByClient[clientLanguage] = clientLanguage;
            changed = true;
        }

        if (Repair(entries, clientLanguage))
            changed = true;

        if (changed)
            Save();
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    /// <summary>Moves a language to the head of a list, where it becomes the primary one.</summary>
    private static void MoveToFront(List<LanguageEntry> entries, GameLanguage language)
    {
        var index = entries.FindIndex(entry => entry.Language == language);
        if (index <= 0)
            return;

        var entry = entries[index];
        entries.RemoveAt(index);
        entries.Insert(0, entry);
    }

    /// <summary>
    /// Rewrites a language list in place so it holds every language exactly once and has the
    /// game's own language enabled, leading the list if it was missing entirely. Returns whether
    /// anything actually changed.
    /// </summary>
    private static bool Repair(List<LanguageEntry> entries, GameLanguage clientLanguage)
    {
        var repaired = new List<LanguageEntry>();
        var seen = new HashSet<GameLanguage>();

        foreach (var entry in entries)
        {
            if (!Enum.IsDefined(entry.Language) || !seen.Add(entry.Language))
                continue;

            repaired.Add(entry.Language == clientLanguage ? entry with { Enabled = true } : entry);
        }

        foreach (var language in Enum.GetValues<GameLanguage>())
        {
            if (!seen.Add(language))
                continue;

            if (language == clientLanguage)
                repaired.Insert(0, new LanguageEntry(language, true));
            else
                repaired.Add(new LanguageEntry(language, false));
        }

        if (repaired.Count == entries.Count && repaired.TrueForAll(entries.Contains))
            return false;

        entries.Clear();
        entries.AddRange(repaired);
        return true;
    }
}
