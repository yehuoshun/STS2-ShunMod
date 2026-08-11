namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     PowersPersist 配置开关。
/// </summary>
public static class PowersPersistConfig
{
    /// <summary>
    ///     开启后，打出 Power 卡后将其从运行牌组中移除（在正常的消耗行为之外）。
    ///     对应 STS1 PowersPersist 模组的可选设置。
    /// </summary>
    public static bool RemovePowerCardsOnPlay => true;

    /// <summary>
    ///     开启后，减益型 Power（以及当前数值为负的增益，如力量=-1）不会带到下一场战斗。
    ///     默认关闭，行为与原版模组一致。
    /// </summary>
    public static bool SkipNegativePowers => false;

    /// <summary>
    ///     开启后，战斗外获得的 Power（如非战斗事件）不会带到下一场战斗。
    ///     默认关闭，行为与原版模组一致。
    /// </summary>
    public static bool SkipNonCombatOriginPowers => false;
}