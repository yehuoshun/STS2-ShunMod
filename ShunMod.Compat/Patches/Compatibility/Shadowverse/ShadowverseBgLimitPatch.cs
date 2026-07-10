using System.Reflection.Emit;
using HarmonyLib;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 解除背景包启用数量限制（7 → 无限）。
///
/// ════════════════════════════════════════════════════════════
///  设计原因
/// ════════════════════════════════════════════════════════════
///
///  为什么用 Transpiler 而不是 Prefix 跳过整个方法？
///  ───────────────────────────────────────────────────────────
///  SetEnabled 不只是检查上限——它还要修改 _preferences 字典、保存配置、
///  加载未装载的背景包（LoadPack）。如果直接用 Prefix 跳过整个方法，
///  需要手动反射操作 _preferences，耦合内部实现且容易漏掉副效应。
///  Transpiler 只替换上限常量 7 为 int.MaxValue，原方法所有逻辑完整保留。
///
///  为什么用 LimitPatchHelper 共享模式？
///  ───────────────────────────────────────────────────────────
///  SkinLimitPatch 和 BgLimitPatch 的代码结构完全一致：
///  Apply → ApplyPatches → OnAssemblyLoad 延迟加载流程完全相同，
///  ReplaceLimitConstant 核心逻辑只有常量值不同（7 vs 14）。
///  共享模式消除 90% 重复代码，设计文档集中到 LimitPatchHelper。
///
/// ════════════════════════════════════════════════════════════
///  反编译确认的限制点
/// ════════════════════════════════════════════════════════════
///
///  ScanInstalledPacks（启动加载）：
///    bool flag6 = flag5 && num >= 7;
///    if (flag6) { flag5 = false; Log.Warn(...AppendFormatted(7)...); }
///
///  SetEnabled（运行时 UI 开关）：
///    bool flag3 = BgPackManager.GetEnabledCount() >= 7;
///    if (flag3) { Log.Warn(...AppendFormatted(7)...); return false; }
///
///  Transpiler 同时替换两处 7：比较操作 + AppendFormatted(7) 日志字符串。
///  日志文字变成 "超出上限 2147483647"，不影响功能。
/// </summary>
public static class ShadowverseBgLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts.UI";
    private const string TargetType = "BgPackManager";

    /// <summary>
    /// 是否已打补丁。Interlocked.CompareExchange 原子守卫。
    /// 引用类型（AppliedFlag）而非 bool，因为 OnAssemblyLoad 局部函数
    /// 需要捕获该字段，C# 不允许局部函数捕获 ref 参数。
    /// 即使多个线程同时读 false，ApplyPatches 的 CompareExchange 保证只执行一次。
    /// </summary>
    private static readonly LimitPatchHelper.AppliedFlag _applied = new();

    /// <summary>
    /// 入口。委托给 LimitPatchHelper 处理查找、延迟加载、打补丁流程。
    /// </summary>
    public static void Apply(Harmony harmony) =>
        LimitPatchHelper.Apply(harmony, ModId, TargetNs, TargetType, "Shadow verse BgLimit",
            _applied, typeof(ShadowverseBgLimitPatch),
            nameof(ScanInstalledPacks_Transpiler), nameof(SetEnabled_Transpiler));

    // ═══════════════════════════════════════════════
    //  Transpiler — 由 LimitPatchHelper 反射调用
    // ═══════════════════════════════════════════════

    /// <summary>
    /// ScanInstalledPacks 的 Transpiler。
    /// 将 IL 中所有常量 7 替换为 int.MaxValue，
    /// 覆盖：num >= 7 的比较 + AppendFormatted(7) 的日志参数。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant7);

    /// <summary>
    /// SetEnabled 的 Transpiler。
    /// 将 IL 中所有常量 7 替换为 int.MaxValue，
    /// 覆盖：GetEnabledCount() >= 7 的比较 + AppendFormatted(7) 的日志参数。
    /// 原方法其他逻辑（修改偏好、加载背景包）不受影响。
    /// </summary>
    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant7);
}