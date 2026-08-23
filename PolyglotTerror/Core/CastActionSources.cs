using System;

namespace PolyglotTerror.Core;

/// <summary>
/// The sheet a cast bar's action id belongs to, numbered as the ActionType the client reports
/// alongside a cast. Each type numbers its rows from one, so a mount id and an action id of the
/// same number name two unrelated things. Kept as plain numbers so this stays free of the game
/// bindings.
/// </summary>
public enum CastNameSource : byte
{
    None = 0,
    Action = 1,
    Item = 2,
    EventItem = 3,
    EventAction = 4,
    GeneralAction = 5,
    BuddyAction = 6,
    MainCommand = 7,
    Companion = 8,
    CraftAction = 9,
    PetAction = 11,
    Mount = 13,
    ChocoboRaceAbility = 16,
    ChocoboRaceItem = 17,
    BgcArmyAction = 19,
    Ornament = 20,
}

public static class CastActionSources
{
    /// <summary>
    /// Anything we cannot name resolves to <see cref="CastNameSource.None"/>, which every caller
    /// reads as "leave the game's own text alone".
    /// </summary>
    public static CastNameSource FromActionType(byte actionType)
        => Enum.IsDefined((CastNameSource)actionType) ? (CastNameSource)actionType : CastNameSource.None;
}
