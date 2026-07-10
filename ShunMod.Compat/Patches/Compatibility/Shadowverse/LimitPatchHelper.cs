using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 限制解除类补丁的共享模式。
///
/// 合并 SkinLimit(14) 和 BgLimit(7) 的完全重复代码：
///   - Apply → ApplyPatches → OnAssemblyLoad 延迟加载
///   - ReplaceLimitConstant 核心替换逻辑
///   - 常量检查（7 / 14）
///
/// ════════════════════════════════════════════════════════════
///  设计原因
/// ════════════════════════════════════════════════════════════
///
///  为什么用 Transpiler 而不是 Prefix？
///  ───────────────────────────────────
///  SetEnabled 原方法内部逻辑不止是检查上限——还要修改 _preferences 字典、
///  加载未装载的包（LoadPack）。如果直接用 Prefix 跳过整个方法，
///  需要手动反射操作 _preferences，耦合内部实现、容易漏掉副效应。
///  Transpiler 只替换上限常量（7/14）为 int.MaxValue，原方法的所有逻辑完整保留。
///  (之前版本确实用了 Prefix，后来改成了 Transpiler)
///
///  为什么用 Interlocked 而不是 lock？
///  ───────────────────────────────────
///  _applied 守卫只有一处写入（ApplyPatches 入口）和一处读取（OnAssemblyLoad
///  快速路径）。Interlocked.CompareExchange 原子操作足以保证单次执行语义，
///  不需要引入锁。OnAssemblyLoad 的 if (_applied) 是快速路径优化——即使竞态
///  通过，ApplyPatches 的 CompareExchange 会兜底，不会重复打补丁。
///
///  为什么需要 AssemblyLoad 延迟加载？
///  ───────────────────────────────────
///  sts2 的模组加载顺序按字母排序，"ShunMod" 可能排在 "Shadowverse" 前面，
///  导致 Apply() 执行时 Shadowverse 的 DLL 尚未加载到 AppDomain 中。
///  AssemblyLoad 事件兜底确保 DLL 加载后自动补打补丁。
/// </summary>
internal static class LimitPatchHelper
{
    /// <summary>
    /// 标准 Apply 入口：
    /// 1. 优先查找目标类型，找到直接打补丁
    /// 2. 找不到则订阅 AssemblyLoad 事件延迟加载
    /// </summary>
    public static void Apply(
        Harmony harmony,
        string modId,
        string targetNs,
        string targetType,
        string friendlyName,
        ref bool applied,
        Type transpilerSource,
        string scanTranspilerName,
        string setEnabledTranspilerName)
    {
        var managerType = FindType(targetNs, targetType);
        if (managerType != null)
        {
            ApplyPatches(harmony, managerType, modId, friendlyName, ref applied,
                transpilerSource, scanTranspilerName, setEnabledTranspilerName);
            return;
        }

        Log.Info($"[{modId}] {friendlyName} not yet loaded, subscribing to AssemblyLoad...");
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        return;

        void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            if (applied) return;
            if (FindType(targetNs, targetType) is { } t)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                ApplyPatches(harmony, t, modId, friendlyName, ref applied,
                    transpilerSource, scanTranspilerName, setEnabledTranspilerName);
            }
        }
    }

    /// <summary>
    /// 对已找到的目标类型应用 Transpiler 补丁。
    /// 线程安全：Interlocked.CompareExchange 保证仅第一个调用者执行补丁逻辑。
    /// </summary>
    private static void ApplyPatches(
        Harmony harmony,
        Type managerType,
        string modId,
        string friendlyName,
        ref bool applied,
        Type transpilerSource,
        string scanTranspilerName,
        string setEnabledTranspilerName)
    {
        if (Interlocked.CompareExchange(ref applied, true, false)) return;

        Log.Info($"[{modId}] {friendlyName}: applying patches to {managerType.FullName}");

        var scanMethod = AccessTools.Method(managerType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(transpilerSource, scanTranspilerName));
            Log.Info($"[{modId}] {friendlyName}: ScanInstalledPacks (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{modId}] {friendlyName}: ScanInstalledPacks method not found!");
        }

        var setEnabledMethod = AccessTools.Method(managerType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(transpilerSource, setEnabledTranspilerName));
            Log.Info($"[{modId}] {friendlyName}: SetEnabled (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{modId}] {friendlyName}: SetEnabled method not found!");
        }
    }

    // ═══════════════════════════════════════════════
    //  核心替换逻辑
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 遍历 IL 指令，将匹配 isConstant 的常量压入替换为 int.MaxValue。
    /// 为什么全部替换而不是定位特定位置？
    ///   目标方法中常量仅出现在两个地方——比较操作和 AppendFormatted 日志参数。
    ///   全部替换是安全的，不影响其他逻辑。
    ///   如果未来游戏更新在方法中引入其他常量，全局替换也自动覆盖。
    /// </summary>
    public static IEnumerable<CodeInstruction> ReplaceLimitConstant(
        IEnumerable<CodeInstruction> instructions,
        Func<CodeInstruction, bool> isConstant)
    {
        foreach (var inst in instructions)
        {
            if (isConstant(inst))
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = int.MaxValue;
            }
            yield return inst;
        }
    }

    // ═══════════════════════════════════════════════
    //  常量检查
    // ═══════════════════════════════════════════════

    public static bool IsConstant7(CodeInstruction inst) =>
        (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte and 7)
        || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int and 7);

    public static bool IsConstant14(CodeInstruction inst) =>
        (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte and 14)
        || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int and 14);

    private static Type? FindType(string ns, string typeName) =>
        CompatibilityPatchUtil.FindType(ns, typeName);
}