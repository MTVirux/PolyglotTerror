using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Game.Gui;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using PolyglotTerror.Core;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace PolyglotTerror.Game;

public sealed record ItemNames(string? Name, string? Category, string? Description);

public sealed record ActionNames(string? Name, string? Description);

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
    private readonly Dictionary<(GameLanguage, uint), string?> addonMemo = new();
    private readonly Dictionary<(GameLanguage, byte, uint), string?> castMemo = new();
    private readonly Dictionary<(GameLanguage, DetailKind, uint), ActionNames> detailMemo = new();

    public static GameLanguage FromClientLanguage(ClientLanguage language) => language switch
    {
        ClientLanguage.Japanese => GameLanguage.Japanese,
        ClientLanguage.German => GameLanguage.German,
        ClientLanguage.French => GameLanguage.French,
        _ => GameLanguage.English,
    };

    /// <summary>
    /// A line of the game's own UI text, so labels the plugin draws read in the same language the
    /// game is already speaking rather than in whatever the plugin was written in.
    /// </summary>
    public string? GetUiText(GameLanguage language, uint addonRowId)
    {
        var key = (language, addonRowId);
        if (addonMemo.TryGetValue(key, out var cached))
            return cached;

        var resolved = Row<Addon>(language, addonRowId, static row => row.Text.ExtractText());
        addonMemo[key] = resolved;
        return resolved;
    }

    public ItemNames GetItem(GameLanguage language, uint hoveredItemId)
    {
        var key = (language, hoveredItemId);
        if (itemMemo.TryGetValue(key, out var cached))
            return cached;

        var resolved = ResolveItem(language, hoveredItemId);
        itemMemo[key] = resolved;
        return resolved;
    }

    /// <summary>
    /// Resolves what a cast bar shows. The id only means an action row when the client says the
    /// cast is an action - mounting, interacting and the rest number their own sheets from one.
    /// </summary>
    public string? GetCastName(GameLanguage language, byte actionType, uint actionId)
    {
        var key = (language, actionType, actionId);
        if (castMemo.TryGetValue(key, out var cached))
            return cached;

        var resolved = ResolveCastName(language, CastActionSources.FromActionType(actionType), actionId);
        castMemo[key] = resolved;
        return resolved;
    }

    /// <summary>
    /// Resolves what an action tooltip shows. Action and trait descriptions live in a parallel
    /// "transient" sheet at the same row id, not on the row the name comes from.
    /// </summary>
    public ActionNames GetActionDetail(GameLanguage language, DetailKind kind, uint actionId)
    {
        var key = (language, kind, actionId);
        if (detailMemo.TryGetValue(key, out var cached))
            return cached;

        var resolved = ResolveActionDetail(language, kind, actionId);
        detailMemo[key] = resolved;
        return resolved;
    }

    public void Clear()
    {
        itemMemo.Clear();
        addonMemo.Clear();
        castMemo.Clear();
        detailMemo.Clear();
    }

    private static string? ResolveCastName(GameLanguage language, CastNameSource source, uint actionId) => source switch
    {
        CastNameSource.Action => Row<LuminaAction>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.Item => Row<Item>(language, ItemIdNormalizer.ToBaseItemId(actionId), static row => row.Name.ExtractText()),
        CastNameSource.EventItem => Row<EventItem>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.EventAction => Row<EventAction>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.GeneralAction => Row<GeneralAction>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.BuddyAction => Row<BuddyAction>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.MainCommand => Row<MainCommand>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.Companion => Row<Companion>(language, actionId, static row => row.Singular.ExtractText()),
        CastNameSource.CraftAction => Row<CraftAction>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.PetAction => Row<PetAction>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.Mount => Row<Mount>(language, actionId, static row => row.Singular.ExtractText()),
        CastNameSource.ChocoboRaceAbility => Row<ChocoboRaceAbility>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.ChocoboRaceItem => Row<ChocoboRaceItem>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.BgcArmyAction => Row<BgcArmyAction>(language, actionId, static row => row.Name.ExtractText()),
        CastNameSource.Ornament => Row<Ornament>(language, actionId, static row => row.Singular.ExtractText()),
        _ => null,
    };

    private static ActionNames ResolveActionDetail(GameLanguage language, DetailKind kind, uint actionId) => kind switch
    {
        DetailKind.Action => new ActionNames(
            Row<LuminaAction>(language, actionId, static row => row.Name.ExtractText()),
            Row<ActionTransient>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.Trait => new ActionNames(
            Row<Trait>(language, actionId, static row => row.Name.ExtractText()),
            Row<TraitTransient>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.GeneralAction => new ActionNames(
            Row<GeneralAction>(language, actionId, static row => row.Name.ExtractText()),
            Row<GeneralAction>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.MainCommand => new ActionNames(
            Row<MainCommand>(language, actionId, static row => row.Name.ExtractText()),
            Row<MainCommand>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.ExtraCommand => new ActionNames(
            Row<ExtraCommand>(language, actionId, static row => row.Name.ExtractText()),
            Row<ExtraCommand>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.BuddyAction => new ActionNames(
            Row<BuddyAction>(language, actionId, static row => row.Name.ExtractText()),
            Row<BuddyAction>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.Companion => new ActionNames(
            Row<Companion>(language, actionId, static row => row.Singular.ExtractText()),
            Row<CompanionTransient>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.Mount => new ActionNames(
            Row<Mount>(language, actionId, static row => row.Singular.ExtractText()),
            Row<MountTransient>(language, actionId, static row => row.Description.ExtractText())),
        DetailKind.Ornament => new ActionNames(
            Row<Ornament>(language, actionId, static row => row.Singular.ExtractText()),
            null),
        _ => new ActionNames(null, null),
    };

    private static string? Row<T>(GameLanguage language, uint rowId, Func<T, string?> pick)
        where T : struct, IExcelRow<T>
        => GetSheet<T>(language)?.GetRowOrDefault(rowId) is { } row ? Usable(pick(row)) : null;

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
