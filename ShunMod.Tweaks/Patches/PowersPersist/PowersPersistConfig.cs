namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     PowersPersist 配置开关，静态属性，运行时可直接切换。
/// </summary>
public static class PowersPersistConfig
{
    /// <summary>
    ///     开启后，打出 Power 卡后将其从运行牌组中移除（在正常的消耗行为之外）。
    /// </summary>
    public static bool RemovePowerCardsOnPlay { get; set; }

    /// <summary>
    ///     开启后，减益型 Power（以及当前数值为负的增益，如力量=-1）不会带到下一场战斗。
    /// </summary>
    public static bool SkipNegativePowers { get; set; }

    /// <summary>
    ///     开启后，战斗外获得的 Power（如非战斗事件）不会带到下一场战斗。
    /// </summary>
    public static bool SkipNonCombatOriginPowers { get; set; }
}