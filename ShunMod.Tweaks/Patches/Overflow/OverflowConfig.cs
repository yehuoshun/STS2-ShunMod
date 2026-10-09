namespace ShunMod.Tweaks.Patches.Overflow;

/// <summary>
///     护盾超限记账配置 — 静态属性，运行时可直接切换。
///     注意：Threshold 属于「面额」定义，同一局内请不要动态修改，
///     否则已存的超限层数价值会随新面额变化。
/// </summary>
public static class OverflowConfig
{
    /// <summary>
    ///     是否启用护盾超限记账。
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    ///     护盾记账阈值：护盾达到该值后，超出部分按每层该值转存为「超限」层数，
    ///     余数留在护盾。默认 9,999,999 —— 刻意低于游戏数值上限 999,999,999，
    ///     保证转换真实发生，同时护盾数字永远不超过 9 位字符。
    /// </summary>
    public static int Threshold { get; set; } = 9_999_999;
}