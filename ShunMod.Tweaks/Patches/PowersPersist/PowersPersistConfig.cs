namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     PowersPersist 配置开关。
/// </summary>
public static class PowersPersistConfig
{
    /// <summary>
    ///     开启后，打出 Power 卡后将其从运行牌组中移除（在正常的消耗行为之外）。
    /// </summary>
    public static bool RemovePowerCardsOnPlay => true;
}