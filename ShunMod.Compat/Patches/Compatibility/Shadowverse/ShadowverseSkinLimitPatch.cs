using HarmonyLib;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14 → 无限）。
///
/// 实现全在 <see cref="LimitPatchHelper"/> 共享模式中，此类仅提供配置参数。
///
/// 反编译确认的限制点：
///   ScanInstalledPacks：num >= 14 时强制禁用后续皮肤（含 140 的日志参数）
///   SetEnabled：GetEnabledCount() >= 14 时拒绝启用
///
/// Transpiler 替换 14/140 为 int.MaxValue，比较和日志字符串同时覆盖。
/// 日志文字仅变为"超出上限 2147483647"，不影响功能。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    // AppliedFlag 而非 bool：OnAssemblyLoad 局部函数需捕获该字段，C# 不允许捕获 ref 参数
    private static readonly LimitPatchHelper.AppliedFlag Applied = new();

    public static void Apply(Harmony harmony) =>
        LimitPatchHelper.Apply(harmony, ModId, TargetNs, TargetType, "Shadow verse SkinLimit",
            Applied, typeof(ShadowverseSkinLimitPatch),
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