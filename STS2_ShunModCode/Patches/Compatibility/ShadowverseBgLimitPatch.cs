using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除背景包启用数量限制（7→无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动加载）：
///        bool flag6 = flag5 &amp;&amp; num &gt;= 7;   // 第 8 个起强制禁用
///   2. SetEnabled（运行时手动开关）：
///        bool flag3 = BgPackManager.GetEnabledCount() &gt;= 7;
///
/// 方案：
///   - ScanInstalledPacks: Transpiler 将 ldc.i4.s 7 替换为 ldc.i4 int.MaxValue
///   - SetEnabled: Prefix 跳过原方法直接写入 _preferences
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

        // ── Patch 2: SetEnabled Prefix — 运行时手动开关冗余保护 ──
        var setEnabledMethod = AccessTools.Method(_bgMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(SetEnabled_Prefix)));
            Log.Info($"[{ModId}] Shadowverse BgLimit: SetEnabled cap bypass patched");
        }
    }

    /// <summary>
    /// Transpiler: 将 ScanInstalledPacks IL 中的 ldc.i4.s 7 替换为 ldc.i4 int.MaxValue。
    /// ldc.i4.s 7 仅用于 num &gt;= 7 比较，不会误伤其他常量。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 7)
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = int.MaxValue;
            }
            yield return inst;
        }
    }

    /// <summary>
    /// SetEnabled Prefix: 启用时跳过原方法直接写入 _preferences。
    /// 禁用操作仍走原方法（不受限）。
    /// </summary>
    private static bool SetEnabled_Prefix(string packId, bool enabled, ref bool __result)
    {
        if (!enabled) return true; // 禁用走原方法

        var prefsField = _bgMgrType!.GetField("_preferences",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (prefsField == null) return true;

        var prefs = (Dictionary<string, bool>)prefsField.GetValue(null)!;
        prefs[packId] = true;
        __result = true;
        return false; // 跳过原方法
    }

    private static Type? FindType()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType($"{TargetNs}.{TargetType}");
            if (t != null) return t;
        }
        return null;
    }
}
