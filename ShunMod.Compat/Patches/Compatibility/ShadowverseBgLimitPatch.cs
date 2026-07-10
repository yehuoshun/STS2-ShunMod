using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除背景包启用数量限制（7→无限）。
///
/// 反编译确认限制在两处（均在 BgPackManager 类中）：
///   1. ScanInstalledPacks（启动加载）：num &gt;= 7 时强制禁用后续背景包
///   2. SetEnabled（运行时 UI 开关）：GetEnabledCount() &gt;= 7 时拒绝启用
///
/// 方案：两个方法都用 Transpiler，替换 IL 中上限常量 7 为 int.MaxValue。
///   覆盖：比较操作 + AppendFormatted&lt;int&gt;(7) 日志字符串。
///   日志文字变成 "超出上限 2147483647"，不影响功能。
///
/// 防时序问题：如果 Apply() 执行时 Shadow verse DLL 尚未加载（模组加载顺序问题），
/// 通过 AppDomain.AssemblyLoad 事件兜底，DLL 加载后自动重试。
/// </summary>
public static class ShadowverseBgLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts.UI";
    private const string TargetType = "BgPackManager";

    private static volatile bool _applied;
    private static readonly Lock ApplyLock = new();

    public static void Apply(Harmony harmony)
    {
        var bgMgrType = FindType();
        if (bgMgrType != null)
        {
            ApplyPatches(harmony, bgMgrType);
            return;
        }

        // 延迟补救：模组加载可能按字母序，Shadow verse DLL 还没进 AppDomain。
        // 订阅 AssemblyLoad 事件，等它的 DLL 加载后再试。
        Log.Info($"[{ModId}] Shadow verse BgPackManager not yet loaded, subscribing to AssemblyLoad...");
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        return;

        void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            if (Volatile.Read(ref _applied)) return;
            if (FindType() is { } t)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                ApplyPatches(harmony, t);
            }
        }
    }

    /// <summary>
    /// 对已找到的 BgPackManager 类型应用 Transpiler 补丁。
    /// 线程安全，防重复。
    /// </summary>
    private static void ApplyPatches(Harmony harmony, Type bgMgrType)
    {
        if (!TryLock()) return;
        Log.Info($"[{ModId}] Shadow verse BgLimit: applying patches to {bgMgrType.FullName}");

        // ── Patch 1: ScanInstalledPacks — num >= 7 → int.MaxValue ──
        var scanMethod = AccessTools.Method(bgMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadow verse BgLimit: ScanInstalledPacks (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{ModId}] Shadow verse BgLimit: ScanInstalledPacks method not found!");
        }

        // ── Patch 2: SetEnabled — GetEnabledCount() >= 7 → int.MaxValue ──
        var setEnabledMethod = AccessTools.Method(bgMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(SetEnabled_Transpiler)));
            Log.Info($"[{ModId}] Shadow verse BgLimit: SetEnabled (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{ModId}] Shadow verse BgLimit: SetEnabled method not found!");
        }
    }

    /// <summary>尝试获取应用锁。返回 false 说明已有其他路径完成补丁。</summary>
    private static bool TryLock()
    {
        lock (ApplyLock)
        {
            if (_applied) return false;
            _applied = true;
            return true;
        }
    }

    // ═══════════════════════════════════════════════
    //  Transpiler
    // ═══════════════════════════════════════════════

    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) => ReplaceLimitConstant(instructions);

    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) => ReplaceLimitConstant(instructions);

    // ═══════════════════════════════════════════════
    //  核心替换逻辑
    // ═══════════════════════════════════════════════

    /// <summary>遍历 IL 指令，将 7 的常量压入替换为 int.MaxValue。</summary>
    private static IEnumerable<CodeInstruction> ReplaceLimitConstant(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (IsConstant7(inst))
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = int.MaxValue;
            }
            yield return inst;
        }
    }

    /// <summary>
    /// 判断 IL 指令是否为上限常量 7。
    /// ldc.i4.s（短格式 ≤127）→ 7；ldc.i4（长格式 &gt;127）→ 7。
    /// 背景包上限只有 7，没有第二种常量。
    /// </summary>
    private static bool IsConstant7(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte and 7)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int and 7);
    }

    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}