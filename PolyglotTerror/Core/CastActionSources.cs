namespace PolyglotTerror.Core;

/// <summary>
/// The sheet a cast bar's action id belongs to. Each type numbers its rows from one, so a mount
/// id and an action id of the same number name two unrelated things.
/// </summary>
public enum CastNameSource
{
    None,
    Action,
    Item,
    EventItem,
    EventAction,
    GeneralAction,
    BuddyAction,
    MainCommand,
    Companion,
    CraftAction,
    PetAction,
    Mount,
    ChocoboRaceAbility,
    ChocoboRaceItem,
    BgcArmyAction,
    Ornament,
}

public static class CastActionSources
{
    /// <summary>
    /// Maps the ActionType the client reports alongside a cast. Kept as plain numbers so this stays
    /// free of the game bindings. Anything we cannot name resolves to <see cref="CastNameSource.None"/>,
    /// which every caller reads as "leave the game's own text alone".
    /// </summary>
    public static CastNameSource FromActionType(byte actionType) => actionType switch
    {
        1 => CastNameSource.Action,
        2 => CastNameSource.Item,
        3 => CastNameSource.EventItem,
        4 => CastNameSource.EventAction,
        5 => CastNameSource.GeneralAction,
        6 => CastNameSource.BuddyAction,
        7 => CastNameSource.MainCommand,
        8 => CastNameSource.Companion,
        9 => CastNameSource.CraftAction,
        11 => CastNameSource.PetAction,
        13 => CastNameSource.Mount,
        16 => CastNameSource.ChocoboRaceAbility,
        17 => CastNameSource.ChocoboRaceItem,
        19 => CastNameSource.BgcArmyAction,
        20 => CastNameSource.Ornament,
        _ => CastNameSource.None,
    };
}
