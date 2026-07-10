using HarmonyLib;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14 → 无限）。
///
/// ════════════════════════════════════════════════════════════
///  设计原因
/// ════════════════════════════════════════════════════════════
///
///  为什么用 Transpiler 而不是 Prefix 跳过整个方法？
///  ───────────────────────────────────────────────────────────
///  SetEnabled 不只是检查上限——它还要修改 _preferences 字典、保存配置、
///  加载未装载的皮肤包（LoadPack）。如果直接用 Prefix 跳过整个方法，
///  需要手动反射操作 _preferences，耦合内部实现且容易漏掉副效应。
///  Transpiler 只替换上限常量 14/140 为 int.MaxValue，原方法所有逻辑
///  完整保留，安全且无副效应。
///
///  为什么用 LimitPatchHelper 共享模式？
///  ───────────────────────────────────────────────────────────
///  BgLimitPatch 和 SkinLimitPatch 的代码结构完全一致：
///  Apply → ApplyPatches → OnAssemblyLoad 延迟加载流程完全相同，
///  ReplaceLimitConstant 核心逻辑只有常量值不同（7 vs 14）。
///  共享模式消除 90% 重复代码，设计文档集中到 LimitPatchHelper。
///
///  为什么 AssemblyLoad 延迟加载？
///  ───────────────────────────────────────────────────────────
///  sts2 的模组加载顺序按字母排序，"ShunMod" 排在 "Shadowverse" 前面，
///  Apply() 执行时 SkinPackManager 可能尚未加载到 AppDomain 中。
///  AssemblyLoad 事件兜底确保 DLL 加载后自动补打补丁。
///
///  为什么 Transpiler 方法用 private？
///  ───────────────────────────────────────────────────────────
///  Harmony 通过反射调用 Transpiler，private 不影响反射。
///  private 限制 API 表面，避免其他代码意外调用。
///  LimitPatchHelper.Apply() 通过 nameof 将方法名传过去，编译期安全。
///
/// ════════════════════════════════════════════════════════════
///  反编译确认的限制点
/// ════════════════════════════════════════════════════════════
///
///  ScanInstalledPacks（启动加载）：
///    bool flag2 = flag && num >= 14;
///    if (flag2) { flag = false; Log.Warn(...AppendFormatted(14)...); }
///
///  SetEnabled（运行时 UI 开关）：
///    bool flag3 = SkinPackManager.GetEnabledCount() >= 14;
///    if (flag3) { Log.Warn(...AppendFormatted(14)...); return false; }
///
///  Transpiler 同时替换两处 14（以及 140 的日志参数）：
///  比较操作 + AppendFormatted 日志字符串。
///  日志文字变成 "超出上限 2147483647"，不影响功能。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    /// <summary>
    /// 是否已打补丁。Interlocked.CompareExchange 原子守卫。
    /// OnAssemblyLoad 快速路径的 if (_applied) 是竞态允许的优化——
    /// 即使多个线程同时读 false，ApplyPatches 的 CompareExchange 保证只执行一次。
    /// </summary>
    private static readonly LimitPatchHelper.AppliedFlag _applied = new();

    /// <summary>
    /// 入口。委托给 LimitPatchHelper 处理查找、延迟加载、打补丁流程。
    /// </summary>
    public static void Apply(Harmony harmony) =>
        LimitPatchHelper.Apply(harmony, ModId, TargetNs, TargetType, "Shadow verse SkinLimit",
            _applied, typeof(ShadowverseSkinLimitPatch),
            nameof(ScanInstalledPacks_Transpiler), nameof(SetEnabled_Transpiler));

    // ═══════════════════════════════════════════════
    //  Transpiler — 由 LimitPatchHelper 反射调用
    // ═══════════════════════════════════════════════

    /// <summary>
    /// ScanInstalledPacks 的 Transpiler。
    /// 将 IL 中所有常量 14 和 140 替换为 int.MaxValue，
    /// 覆盖：num >= 14 的比较 + AppendFormatted(14) 的日志参数。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant14);

    /// <summary>
    /// SetEnabled 的 Transpiler。
    /// 将 IL 中所有常量 14 和 140 替换为 int.MaxValue，
    /// 覆盖：GetEnabledCount() >= 14 的比较 + AppendFormatted(14) 的日志参数。
    /// 原方法其他逻辑（修改偏好、加载皮肤包）不受影响。
    /// </summary>
    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant14);
}