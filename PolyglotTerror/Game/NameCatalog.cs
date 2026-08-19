using System;
using System.Collections.Generic;
using Dalamud.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using PolyglotTerror.Core;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace PolyglotTerror.Game;

public sealed record ItemNames(string? Name, string? Category, string? Description);

public sealed class NameCatalog
{
    private static readonly Dictionary<GameLanguage, ClientLanguage> ClientLanguages = new()
    {
        { GameLanguage.English, ClientLanguage.English },
        { GameLanguage.Japanese, ClientLanguage.Japanese },
        { GameLanguage.German, ClientLanguage.German },
        { GameLanguage.French, ClientLanguage.French },
    };

    private readonly Dictionary<(GameLanguage, uint), ItemNames> itemMemo = new();
    private readonly Dictionary<(GameLanguage, uint), string?> actionMemo = new();

    public static GameLanguage FromClientLanguage(ClientLanguage language) => language switch
    {
        ClientLanguage.Japanese => GameLanguage.Japanese,
        ClientLanguage.German => GameLanguage.German,
        ClientLanguage.French => GameLanguage.French,
        _ => GameLanguage.English,
    };

    public ItemNames GetItem(GameLanguage language, uint hoveredItemId)
    {
        var key = (language, hoveredItemId);
        if (itemMemo.TryGetValue(key, out var cached))
            return cached;

        var resolved = ResolveItem(language, hoveredItemId);
        itemMemo[key] = resolved;
        return resolved;
    }

    public string? GetAction(GameLanguage language, uint actionId)
    {
        var key = (language, actionId);
        if (actionMemo.TryGetValue(key, out var cached))
            return cached;

        var sheet = GetSheet<LuminaAction>(language);
        var resolved = sheet?.GetRowOrDefault(actionId) is { } row ? Usable(row.Name.ExtractText()) : null;
        actionMemo[key] = resolved;
        return resolved;
    }

    public void Clear()
    {
        itemMemo.Clear();
        actionMemo.Clear();
    }

    private static ItemNames ResolveItem(GameLanguage language, uint hoveredItemId)
    {
        if (ItemIdNormalizer.IsEventItem(hoveredItemId))
        {
            var eventSheet = GetSheet<EventItem>(language);
            return eventSheet?.GetRowOrDefault(hoveredItemId) is { } eventRow
                ? new ItemNames(Usable(eventRow.Name.ExtractText()), null, null)
                : new ItemNames(null, null, null);
        }

        var sheet = GetSheet<Item>(language);
        if (sheet?.GetRowOrDefault(ItemIdNormalizer.ToBaseItemId(hoveredItemId)) is not { } row)
            return new ItemNames(null, null, null);

        var category = row.ItemUICategory.ValueNullable is { } uiCategory
            ? Usable(uiCategory.Name.ExtractText())
            : null;

        return new ItemNames(
            Usable(row.Name.ExtractText()),
            category,
            Usable(row.Description.ExtractText()));
    }

    private static ExcelSheet<T>? GetSheet<T>(GameLanguage language)
        where T : struct, IExcelRow<T>
        => ClientLanguages.TryGetValue(language, out var clientLanguage)
            ? Plugin.DataManager.GetExcelSheet<T>(clientLanguage)
            : null;

    // Unreleased content ships as blank rows or "_rsv_" placeholders.
    private static string? Usable(string? text)
        => string.IsNullOrWhiteSpace(text) || text.StartsWith("_rsv_", StringComparison.Ordinal)
            ? null
            : text;
}
