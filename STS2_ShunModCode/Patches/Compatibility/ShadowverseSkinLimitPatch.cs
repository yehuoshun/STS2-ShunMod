using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14 → 无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动时扫描）：num &gt;= 14 时强制禁用后续皮肤
///   2. SetEnabled（运行时 UI 开关）：GetEnabledCount() &gt;= 14 时拒绝启用
///
/// 方案：两个方法都用 Transpiler，替换 IL 中所有常量 14 为 int.MaxValue。
///   覆盖：比较操作（num &gt;= 14 / GetEnabledCount() &gt;= 14）+
///         AppendFormatted&lt;int&gt;(14) 日志字符串。
///   日志文字变成 "超出上限 2147483647"，不影响功能。
///
/// 纯 IL 操作，不需要反射访问 _preferences 字段，不依赖任何字段类型兼容性。
/// v2.0: SetEnabled 从 Prefix 改为 Transpiler，消除反射静默失败的隐患。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    public static void Apply(Harmony harmony)
    {
        var skinMgrType = FindType();
        if (skinMgrType == null)
        {
            Log.Info($"[{ModId}] Shadowverse SkinPackManager not detected, skipping skin limit patch");
            return;
        }

        // ── Patch 1: ScanInstalledPacks Transpiler — 14 → int.MaxValue ──
        var scanMethod = AccessTools.Method(skinMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: ScanInstalledPacks cap removed (Transpiler, unlimited)");
        }

        // ── Patch 2: SetEnabled Transpiler — 14 → int.MaxValue ──
        // Transpiler 比 Prefix 更可靠：不需要反射 _preferences 字段，
        // 直接把 >= 14 改成 >= int.MaxValue，原方法流程不变。
        var setEnabledMethod = AccessTools.Method(skinMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(SetEnabled_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: SetEnabled cap removed (Transpiler, unlimited)");
        }
    }

    // ═══════════════════════════════════════════════
    //  ScanInstalledPacks — Transpiler
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 替换 ScanInstalledPacks IL 中所有常量 14 为 int.MaxValue。
    /// 覆盖：num &gt;= 14 的比较 + AppendFormatted&lt;int&gt;(14) 的日志字符串。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceLimitConstant(instructions);
    }

    // ═══════════════════════════════════════════════
    //  SetEnabled — Transpiler
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 替换 SetEnabled IL 中所有常量 14 为 int.MaxValue。
    /// 覆盖：GetEnabledCount() &gt;= 14 的比较 + AppendFormatted&lt;int&gt;(14) 的日志字符串。
    /// </summary>
    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceLimitConstant(instructions);
    }

    // ═══════════════════════════════════════════════
    //  核心替换逻辑
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 遍历 IL 指令，将所有值为 14 的常量压入指令替换为 int.MaxValue。
    /// 匹配 ldc.i4.s 14（短格式，≤127 的常量）和 ldc.i4 14（长格式）。
    /// </summary>
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
    /// 判断 IL 指令是否为常量 14（ldc.i4.s 14 或 ldc.i4 14）。
    /// </summary>
    private static bool IsConstant14(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 14)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && i == 14);
    }

    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}
