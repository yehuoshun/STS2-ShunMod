using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14 → 无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动时扫描）：num &gt;= 14 时强制禁用后续皮肤
///   2. SetEnabled（运行时 UI 开关）：GetEnabledCount() &gt;= 14 时拒绝启用
///
/// 方案：两个方法都用 Transpiler，替换 IL 中所有皮肤上限常量（14 或 140）为 int.MaxValue。
///   覆盖：比较操作 + AppendFormatted&lt;int&gt;(14/140) 日志字符串。
///   日志文字变成 "超出上限 2147483647"，不影响功能。
///
/// 防时序问题：如果 Apply() 执行时 Shadow verse DLL 尚未加载（模组加载顺序问题），
/// 通过 AppDomain.AssemblyLoad 事件兜底，DLL 加载后自动重试。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    private static bool _applied;
    private static readonly object _applyLock = new();

    public static void Apply(Harmony harmony)
    {
        var skinMgrType = FindType();
        if (skinMgrType != null)
        {
            ApplyPatches(harmony, skinMgrType);
            return;
        }

        // 延迟补救：模组加载可能按字母序，Shadow verse DLL 还没进 AppDomain。
        // 订阅 AssemblyLoad 事件，等它的 DLL 加载后再试。
        Log.Info($"[{ModId}] Shadow verse SkinPackManager not yet loaded, subscribing to AssemblyLoad...");
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        return;

        void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            if (_applied) return;
            if (FindType() is { } t)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                ApplyPatches(harmony, t);
            }
        }
    }

    /// <summary>
    /// 对已找到的 SkinPackManager 类型应用 Transpiler 补丁。
    /// 线程安全，防重复。
    /// </summary>
    private static void ApplyPatches(Harmony harmony, Type skinMgrType)
    {
        if (!TryLock()) return;
        Log.Info($"[{ModId}] Shadow verse SkinLimit: applying patches to {skinMgrType.FullName}");

        // ── Patch 1: ScanInstalledPacks — num >= 14/140 → int.MaxValue ──
        var scanMethod = AccessTools.Method(skinMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadow verse SkinLimit: ScanInstalledPacks (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{ModId}] Shadow verse SkinLimit: ScanInstalledPacks method not found!");
        }

        // ── Patch 2: SetEnabled — GetEnabledCount() >= 14/140 → int.MaxValue ──
        var setEnabledMethod = AccessTools.Method(skinMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(SetEnabled_Transpiler)));
            Log.Info($"[{ModId}] Shadow verse SkinLimit: SetEnabled (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{ModId}] Shadow verse SkinLimit: SetEnabled method not found!");
        }
    }

    /// <summary>尝试获取应用锁。返回 false 说明已有其他路径完成补丁。</summary>
    private static bool TryLock()
    {
        lock (_applyLock)
        {
            if (_applied) return false;
            _applied = true;
            return true;
        }
    }

    // ═══════════════════════════════════════════════
    //  Transpilers
    // ═══════════════════════════════════════════════

    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) => ReplaceLimitConstant(instructions);

    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) => ReplaceLimitConstant(instructions);

    // ═══════════════════════════════════════════════
    //  核心替换逻辑
    // ═══════════════════════════════════════════════

    /// <summary>遍历 IL 指令，将 14 或 140 的常量压入替换为 int.MaxValue。</summary>
    private static IEnumerable<CodeInstruction> ReplaceLimitConstant(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (IsConstant14(inst))
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = int.MaxValue;
            }
            yield return inst;
        }
    }

    /// <summary>
    /// 判断 IL 指令是否为皮肤上限常量（14 或 140）。
    /// ldc.i4.s（短格式 ≤127）→ 14；ldc.i4（长格式 &gt;127）→ 140。
    /// </summary>
    private static bool IsConstant14(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 14)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && (i == 14 || i == 140));
    }

    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}
