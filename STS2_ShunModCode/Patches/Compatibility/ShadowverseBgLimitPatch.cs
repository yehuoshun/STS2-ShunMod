using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除背景包启用数量限制（7→无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动加载）：
///        bool flag6 = flag5 &amp;&amp; num &gt;= 7;   // 第 8 个起强制禁用
///   2. SetEnabled（运行时 UI 开关）：
///        bool flag3 = GetEnabledCount() &gt;= 7;
///
/// 方案：两个方法都上 Transpiler，将 7 替换为 int.MaxValue。
/// 同时匹配 ldc.i4.s（短格式）和 ldc.i4（长格式），兼容不同 Roslyn 版本。
/// 纯反射，不引用 shadowverse.dll。
/// </summary>
public static class ShadowverseBgLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts.UI";
    private const string TargetType = "BgPackManager";

    private static Type? _bgMgrType;

    public static void Apply(Harmony harmony)
    {
        _bgMgrType = FindType();
        if (_bgMgrType == null)
        {
            Log.Info($"[{ModId}] Shadowverse BgPackManager not detected, skipping background limit patch");
            return;
        }

        // ── Patch 1: ScanInstalledPacks Transpiler — 7 → int.MaxValue ──
        var scanMethod = AccessTools.Method(_bgMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse BgLimit: ScanInstalledPacks cap removed (7→unlimited)");
        }

        // ── Patch 2: SetEnabled Transpiler — GetEnabledCount() >= 7 ──
        var setEnabledMethod = AccessTools.Method(_bgMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(SetEnabled_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse BgLimit: SetEnabled cap removed (7→unlimited)");
        }
    }

    /// <summary>
    /// Transpiler: 将 ScanInstalledPacks IL 中的 7 常量替换为 int.MaxValue。
    /// 同时匹配 ldc.i4.s 7（短格式）和 ldc.i4 7（长格式），
    /// 因为不同 C# 编译器版本可能生成不同的 IL 指令。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
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
    /// SetEnabled Transpiler: 运行时 GetEnabledCount() &gt;= 7 也补上。
    /// UI 开关背景包会走 SetEnabled，双重保障。
    /// </summary>
    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
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

    /// <summary>判断 IL 指令是否为常量 7（短格式或长格式）</summary>
    private static bool IsConstant7(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 7)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && i == 7);
    }

    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}
