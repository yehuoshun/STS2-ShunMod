namespace ShunMod.Tweaks.PowersPersist.Config;

/// <summary>
///     PowersPersist configuration toggles.
///     All properties are static so they can be toggled at runtime.
/// </summary>
public static class PowersPersistConfig
{
    /// <summary>
    ///     When true, power cards are removed from the run deck after being played
    ///     (in addition to their normal exhaust-on-play behaviour). Matches the
    ///     optional setting in the original Slay the Spire 1 "Powers Persist" mod.
    /// </summary>
    public static bool RemovePowerCardsOnPlay { get; set; } = true;

    /// <summary>
    ///     When true, debuff-type powers (and buffs whose current amount has gone
    ///     negative, like Strength=-1 from Shrink) are NOT carried over to the
    ///     next combat. Default off, so behaviour matches the original mod.
    /// </summary>
    public static bool SkipNegativePowers { get; set; } = false;

    /// <summary>
    ///     When true, powers gained outside an active combat (e.g. from
    ///     non-combat events) are NOT carried over to the next combat. Default
    ///     off, so behaviour matches the original mod.
    /// </summary>
    public static bool SkipNonCombatOriginPowers { get; set; } = false;
}