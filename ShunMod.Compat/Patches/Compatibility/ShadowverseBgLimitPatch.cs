using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除背景包启用数量限制（7→无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动加载）：num &gt;= 7 时强制禁用后续背景包
///   2. SetEnabled（运行时 UI 开关）：GetEnabledCount() &gt;= 7 时拒绝启用
///
/// 方案：
///   ScanInstalledPacks → Transpiler: 7 替换为 int.MaxValue
///     （覆盖 num &gt;= 7 的比较 + 日志中的 AppendFormatted&lt;int&gt;(7)）
///   SetEnabled → Prefix: 跳过原方法，始终允许启用/禁用，__result=true
///     （Prefix 更可靠，不依赖 IL 布局）
///
/// 纯反射，不引用 shadowverse.dll。
/// </summary>
public static class ShadowverseBgLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts.UI";
    private const string TargetType = "BgPackManager";

    private const string PrefsFieldName = "_preferences";

    private static Type? _bgMgrType;
    private static FieldInfo? _prefsField;

    public static void Apply(Harmony harmony)
    {
        _bgMgrType = CompatibilityPatchUtil.FindPatchType(ModId, TargetNs, TargetType);
        if (_bgMgrType == null) return;

        // 发现 _preferences 字段
        _prefsField = _bgMgrType.GetField(PrefsFieldName,
            BindingFlags.NonPublic | BindingFlags.Static);

        // ── Patch 1: ScanInstalledPacks Transpiler — 7 → int.MaxValue ──
        var scanMethod = AccessTools.Method(_bgMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadow verse BgLimit: ScanInstalledPacks cap removed (7→unlimited)");
        }

        // ── Patch 2: SetEnabled Prefix — 跳过原方法 ──
        var setEnabledMethod = AccessTools.Method(_bgMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(SetEnabled_Prefix)));
            Log.Info($"[{ModId}] Shadow verse BgLimit: SetEnabled cap removed (Prefix, unlimited)");
        }
    }

    // ═══════════════════════════════════════════════
    //  ScanInstalledPacks — Transpiler
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Transpiler: 将 ScanInstalledPacks IL 中的 7 常量替换为 int.MaxValue。
    /// 同时匹配 ldc.i4.s 7（短格式）和 ldc.i4 7（长格式）。
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

    // ═══════════════════════════════════════════════
    //  SetEnabled — Prefix（跳过原方法）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Prefix: 跳过 BgPackManager.SetEnabled 原方法。
    /// 直接修改 _preferences 字典并返回 true。
    /// </summary>
    /// <remarks>
    /// __result 是 Harmony2 保留参数名，IDE 命名规则警告由 #pragma 抑制。
    /// </remarks>
#pragma warning disable IDE1006
    private static bool SetEnabled_Prefix(string packId, bool enabled, ref bool __result)
#pragma warning restore IDE1006
    {
        try
        {
            if (_prefsField != null)
            {
                var prefs = _prefsField.GetValue(null) as IDictionary<string, bool>;
                if (prefs != null)
                {
                    prefs[packId] = enabled;
                    __result = true;
                    return false; // 跳过原方法
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[{ModId}] BgLimit SetEnabled Prefix failed, falling through to original: {ex.Message}");
        }

        return true; // 走原方法（带限制）
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    /// <summary>判断 IL 指令是否为常量 7（短格式或长格式）</summary>
    private static bool IsConstant7(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 7)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && i == 7);
    }


}
