namespace ShunMod.Tweaks.Patches.Overflow;

/// <summary>
///     超限生命（命银行）配置 — 静态属性，运行时可直接切换。
///     规则：1 层 = 一条命（拆命瞬间回满血）。
/// </summary>
public static class OverflowLifeConfig
{
    /// <summary>
    ///     是否启用超限生命（命银行）。
    /// </summary>
    public static bool Enabled { get; set; } = true;
}