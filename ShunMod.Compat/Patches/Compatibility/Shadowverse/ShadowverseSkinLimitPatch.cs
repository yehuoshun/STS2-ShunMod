using HarmonyLib;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14 → 无限）。
/// 实现全在 <see cref="LimitPatchHelper"/> 共享模式中，此类仅提供配置参数。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动时扫描）：num &gt;= 14 时强制禁用后续皮肤
///   2. SetEnabled（运行时 UI 开关）：GetEnabledCount() &gt;= 14 时拒绝启用
///
/// 方案：两个方法都用 Transpiler，替换 IL 中所有皮肤上限常量（14 或 140）为 int.MaxValue。
///   覆盖：比较操作 + AppendFormatted&lt;int&gt;(14/140) 日志字符串。
///   日志文字变成 "超出上限 2147483647"，不影响功能。
///
/// 防时序问题：如果 Apply() 执行时 Shadowverse DLL 尚未加载（模组加载顺序问题），
/// 通过 AppDomain.AssemblyLoad 事件兜底，DLL 加载后自动重试。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    private static bool _applied;

    public static void Apply(Harmony harmony) =>
        LimitPatchHelper.Apply(harmony, ModId, TargetNs, TargetType, "Shadow verse SkinLimit",
            ref _applied, typeof(ShadowverseSkinLimitPatch),
            nameof(ScanInstalledPacks_Transpiler), nameof(SetEnabled_Transpiler));

    // ═══════════════════════════════════════════════
    //  Transpiler — 由 LimitPatchHelper 反射调用
    // ═══════════════════════════════════════════════

    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant14);

    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant14);
}