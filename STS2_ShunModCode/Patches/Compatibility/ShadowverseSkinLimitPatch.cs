using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14→无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动时扫描）：
///        bool flag3 = flag2 &amp;&amp; num &gt;= 14;   // 超过第 14 个直接强制禁用
///   2. SetEnabled（运行时 UI 开关）：
///        bool flag3 = GetEnabledCount() &gt;= 14;
///
/// 方案：两个方法都上 Transpiler，将 14 替换为 int.MaxValue。
/// 同时匹配 ldc.i4.s（短格式）和 ldc.i4（长格式），兼容不同 Roslyn 版本。
/// 纯反射，不引用 shadowverse.dll。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    private static Type? _skinMgrType;

    public static void Apply(Harmony harmony)
    {
        _skinMgrType = FindType();
        if (_skinMgrType == null)
        {
            Log.Info($"[{ModId}] Shadowverse SkinPackManager not detected, skipping skin limit patch");
            return;
        }

        // ── Patch 1: ScanInstalledPacks Transpiler — 14 → int.MaxValue ──
        var scanMethod = AccessTools.Method(_skinMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: ScanInstalledPacks cap removed (14→unlimited)");
        }

        // ── Patch 2: SetEnabled Transpiler — 运行时 GetEnabledCount() >= 14 ──
        var setEnabledMethod = AccessTools.Method(_skinMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(SetEnabled_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: SetEnabled cap removed (14→unlimited)");
        }
    }

    /// <summary>
    /// Transpiler: 将 ScanInstalledPacks IL 中的 14 常量替换为 int.MaxValue。
    /// 同时匹配 ldc.i4.s 14（短格式）和 ldc.i4 14（长格式），
    /// 因为不同 C# 编译器版本可能生成不同的 IL 指令。
    /// 仅用于 num &gt;= 14 的比较，字符串插值的 14 会经过 box 不会误伤。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
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
    /// Transpiler: 将 SetEnabled IL 中的 GetEnabledCount() &gt;= 14 替换为 int.MaxValue。
    /// 双重保障：即使 ScanInstalledPacks 的 transpiler 生效后，
    /// 运行时通过 UI 开关皮肤也会走 SetEnabled 的 GetEnabledCount() &gt;= 14 检查。
    /// 冗余保护，避免极端情况漏掉。
    /// </summary>
    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
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

    /// <summary>判断 IL 指令是否为常量 14（短格式或长格式）</summary>
    private static bool IsConstant14(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 14)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && i == 14);
    }

    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}
