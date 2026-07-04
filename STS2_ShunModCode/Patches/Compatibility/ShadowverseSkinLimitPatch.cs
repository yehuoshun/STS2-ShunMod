using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14→无限）。
///
/// 反编译分析确认限制在 ScanInstalledPacks（启动时扫描），而非 SetEnabled：
///   bool flag2 = preferences[packId].Enabled;
///   bool flag3 = flag2 && num >= 14;   // ← 超过第 14 个直接强制禁用
///   if (flag3) { flag2 = false; ... }
///
/// 方案：Transpiler 将 ldc.i4.s 14 替换为 ldc.i4 int.MaxValue。
/// 保留 SetEnabled prefix 作为运行时手动开关的冗余保护。
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

        // ── Patch 2: SetEnabled Prefix — 运行时冗余保护 ──
        var setEnabledMethod = AccessTools.Method(_skinMgrType, "SetEnabled");
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(SetEnabled_Prefix)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: SetEnabled cap bypass patched");
        }
    }

    /// <summary>
    /// Transpiler: 将 ScanInstalledPacks IL 中的 ldc.i4.s 14 替换为 ldc.i4 int.MaxValue。
    /// ldc.i4.s 14 仅用于 num &gt;= 14 比较（字符串插值的 14 会 box 为 ldc.i4 14），不会误伤。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 14)
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = int.MaxValue;
            }
            yield return inst;
        }
    }

    /// <summary>
    /// SetEnabled Prefix: 启用时跳过原方法直接写入 _preferences。
    /// 禁用操作仍走原方法。
    /// </summary>
    private static bool SetEnabled_Prefix(string packId, bool enabled, ref bool __result)
    {
        if (!enabled) return true; // 禁用走原方法

        var prefsField = _skinMgrType!.GetField("_preferences",
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
