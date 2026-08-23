using System;
using System.Collections.Generic;
using Dalamud.Game;
using Dalamud.Game.Gui;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using PolyglotTerror.Core;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace PolyglotTerror.Game;

public sealed record ItemNames(string? Name, string? Category, string? Description);

public sealed record ActionNames(string? Name, string? Category, string? Description);

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

        var resolved = Row<Addon>(language, addonRowId, static row => row.Text);
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
        CastNameSource.Action => Row<LuminaAction>(language, actionId, static row => row.Name),
        CastNameSource.Item => Row<Item>(language, ItemIdNormalizer.ToBaseItemId(actionId), static row => row.Name),
        CastNameSource.EventItem => Row<EventItem>(language, actionId, static row => row.Name),
        CastNameSource.EventAction => Row<EventAction>(language, actionId, static row => row.Name),
        CastNameSource.GeneralAction => Row<GeneralAction>(language, actionId, static row => row.Name),
        CastNameSource.BuddyAction => Row<BuddyAction>(language, actionId, static row => row.Name),
        CastNameSource.MainCommand => Row<MainCommand>(language, actionId, static row => row.Name),
        CastNameSource.Companion => Row<Companion>(language, actionId, static row => row.Singular),
        CastNameSource.CraftAction => Row<CraftAction>(language, actionId, static row => row.Name),
        CastNameSource.PetAction => Row<PetAction>(language, actionId, static row => row.Name),
        CastNameSource.Mount => Row<Mount>(language, actionId, static row => row.Singular),
        CastNameSource.ChocoboRaceAbility => Row<ChocoboRaceAbility>(language, actionId, static row => row.Name),
        CastNameSource.ChocoboRaceItem => Row<ChocoboRaceItem>(language, actionId, static row => row.Name),
        CastNameSource.BgcArmyAction => Row<BgcArmyAction>(language, actionId, static row => row.Name),
        CastNameSource.Ornament => Row<Ornament>(language, actionId, static row => row.Singular),
        _ => null,
    };

    private static ActionNames ResolveActionDetail(GameLanguage language, DetailKind kind, uint actionId) => kind switch
    {
        DetailKind.Action => ResolveAction(language, actionId),
        DetailKind.Trait => new ActionNames(
            Row<Trait>(language, actionId, static row => row.Name),
            null,
            Row<TraitTransient>(language, actionId, static row => row.Description)),
        DetailKind.GeneralAction => new ActionNames(
            Row<GeneralAction>(language, actionId, static row => row.Name),
            null,
            Row<GeneralAction>(language, actionId, static row => row.Description)),
        DetailKind.MainCommand => new ActionNames(
            Row<MainCommand>(language, actionId, static row => row.Name),
            null,
            Row<MainCommand>(language, actionId, static row => row.Description)),
        DetailKind.ExtraCommand => new ActionNames(
            Row<ExtraCommand>(language, actionId, static row => row.Name),
            null,
            Row<ExtraCommand>(language, actionId, static row => row.Description)),
        DetailKind.BuddyAction => new ActionNames(
            Row<BuddyAction>(language, actionId, static row => row.Name),
            null,
            Row<BuddyAction>(language, actionId, static row => row.Description)),
        DetailKind.Companion => new ActionNames(
            Row<Companion>(language, actionId, static row => row.Singular),
            null,
            Row<CompanionTransient>(language, actionId, static row => row.Description)),
        DetailKind.Mount => new ActionNames(
            Row<Mount>(language, actionId, static row => row.Singular),
            null,
            Row<MountTransient>(language, actionId, static row => row.Description)),
        DetailKind.Ornament => new ActionNames(
            Row<Ornament>(language, actionId, static row => row.Singular),
            null,
            null),
        _ => new ActionNames(null, null, null),
    };

    /// <summary>
    /// Resolves a combat action, whose category - ability, weaponskill, spell - lives on a sheet of
    /// its own. No other detail sheet carries a category the game gives a name to.
    /// </summary>
    private static ActionNames ResolveAction(GameLanguage language, uint actionId)
    {
        if (GetSheet<LuminaAction>(language)?.GetRowOrDefault(actionId) is not { } row)
            return new ActionNames(null, null, null);

        var category = row.ActionCategory.ValueNullable is { } actionCategory
            ? Usable(Text(language, actionCategory.Name))
            : null;

        return new ActionNames(
            Usable(Text(language, row.Name)),
            category,
            Row<ActionTransient>(language, actionId, static transient => transient.Description));
    }

    private static string? Row<T>(GameLanguage language, uint rowId, Func<T, ReadOnlySeString> pick)
        where T : struct, IExcelRow<T>
        => GetSheet<T>(language)?.GetRowOrDefault(rowId) is { } row ? Usable(Text(language, pick(row))) : null;

    /// <summary>
    /// The text the game itself would print. Descriptions keep whole paragraphs behind If macros -
    /// level and job conditions, mostly - and reading the raw string drops every one of them.
    /// </summary>
    private static string? Text(GameLanguage language, ReadOnlySeString text)
        => ClientLanguages.TryGetValue(language, out var clientLanguage)
            ? Plugin.SeStringEvaluator.Evaluate(text, default, clientLanguage).ExtractText()
            : text.ExtractText();

    private static ItemNames ResolveItem(GameLanguage language, uint hoveredItemId)
    {
        if (ItemIdNormalizer.IsEventItem(hoveredItemId))
        {
            var eventSheet = GetSheet<EventItem>(language);
            return eventSheet?.GetRowOrDefault(hoveredItemId) is { } eventRow
                ? new ItemNames(Usable(Text(language, eventRow.Name)), null, null)
                : new ItemNames(null, null, null);
        }

        var sheet = GetSheet<Item>(language);
        if (sheet?.GetRowOrDefault(ItemIdNormalizer.ToBaseItemId(hoveredItemId)) is not { } row)
            return new ItemNames(null, null, null);

        var category = row.ItemUICategory.ValueNullable is { } uiCategory
            ? Usable(Text(language, uiCategory.Name))
            : null;

        return new ItemNames(
            Usable(Text(language, row.Name)),
            category,
            Usable(Text(language, row.Description)));
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
