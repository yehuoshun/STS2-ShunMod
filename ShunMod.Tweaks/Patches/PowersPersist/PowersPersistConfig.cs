using MegaCrit.Sts2.Core.Models.Powers;

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

    /// <summary>
    ///     Power 黑名单：这些 Power 不会持久化到下一场战斗。
    ///     用于隔离持有战斗内临时状态（如指定要复制的目标卡）的 Power——
    ///     跨战斗重连后其内部引用失效，会导致空引用（例如 Nightmare 的 BeforeHandDraw）。
    /// </summary>
    public static HashSet<Type> PowerBlacklist { get; } = new()
    {
        typeof(NightmarePower),
    };
}
