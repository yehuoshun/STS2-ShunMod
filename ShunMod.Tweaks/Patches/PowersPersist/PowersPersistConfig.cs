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

    /// <summary>
    ///     开启后，只持久化 PersistWhitelist 白名单中的 Power；
    ///     关闭时持久化所有 Power（含数值堆叠型，跨战斗复利自担）。
    ///     默认开启：配合 DefaultExcludedStatPowers，默认只排除属性修正型，其余照旧保留。
    /// </summary>
    public static bool UsePersistWhitelist { get; set; } = true;

    /// <summary>
    ///     Power 持久化白名单：UsePersistWhitelist 开启时，
    ///     在白名单里的 Power 永远保留（即使同时命中 DefaultExcludedStatPowers）。
    ///     不放任何内容的默认行为 = 只排除 DefaultExcludedStatPowers，其余全部照旧保留。
    ///     想额外硬保留某些 Power（如每回合触发/光环型）时自行 Add。
    /// </summary>
    public static HashSet<Type> PersistWhitelist { get; } = new();

    /// <summary>
    ///     默认排除的「属性修正型」Power：跨战斗保留会永久叠加属性（力量/敏捷/专注），
    ///     必然数值滚雪球，所以默认不持久化。
    ///     需要保留时加进 PersistWhitelist 即可覆盖。
    /// </summary>
    public static HashSet<Type> DefaultExcludedStatPowers { get; } = new()
    {
        typeof(StrengthPower),
        typeof(DexterityPower),
        typeof(FocusPower),
        typeof(TemporaryStrengthPower),
        typeof(TemporaryDexterityPower),
        typeof(TemporaryFocusPower),
    };
}
