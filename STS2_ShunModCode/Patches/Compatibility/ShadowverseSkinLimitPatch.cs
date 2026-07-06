using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14→无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动时扫描）：num &gt;= 14 时强制禁用后续皮肤
///   2. SetEnabled（运行时 UI 开关）：GetEnabledCount() &gt;= 14 时拒绝启用
///
/// 方案：
///   ScanInstalledPacks → Transpiler: num >= 14 中的 14 替换为 int.MaxValue
///     （也覆盖 log 中的 AppendFormatted&lt;int&gt;(14)，不影响功能）
///   SetEnabled → Prefix: 跳过原方法，始终允许启用/禁用，__result=true
///     （Prefix 比 Transpiler 更可靠，不依赖 IL 布局）
///
/// 纯反射，不引用 shadowverse.dll。
/// 修复：SetEnabled 改用 Prefix 避免 Transpiler 对 GetEnabledCount() >= 14 匹配失败的风险。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    private const string PrefsFieldName = "_preferences";

    private static Type? _skinMgrType;
    private static FieldInfo? _prefsField;

    public static void Apply(Harmony harmony)
    {
        _skinMgrType = FindType();
        if (_skinMgrType == null)
        {
            Log.Info($"[{ModId}] Shadowverse SkinPackManager not detected, skipping skin limit patch");
            return;
        }

        // 发现 _preferences 字段（用于 Prefix 修改）
        _prefsField = _skinMgrType.GetField(PrefsFieldName,
            BindingFlags.NonPublic | BindingFlags.Static);

        // ── Patch 1: ScanInstalledPacks Transpiler — 14 → int.MaxValue ──
        var scanMethod = AccessTools.Method(_skinMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: ScanInstalledPacks cap removed (14→unlimited)");
        }

        // ── Patch 2: SetEnabled Prefix — 跳过原方法 — 直接返回 true ──
        // Prefix 比 Transpiler 更可靠：不依赖 GetEnabledCount() >= 14 的 IL 布局
        var setEnabledMethod = AccessTools.Method(_skinMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(SetEnabled_Prefix)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: SetEnabled cap removed (Prefix, unlimited)");
        }
    }

    // ═══════════════════════════════════════════════
    //  ScanInstalledPacks — Transpiler
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Transpiler: 将 ScanInstalledPacks IL 中的 14 常量替换为 int.MaxValue。
    /// 同时匹配 ldc.i4.s 14（短格式）和 ldc.i4 14（长格式）。
    /// 覆盖：num &gt;= 14 的比较 + AppendFormatted&lt;int&gt;(14) 的日志字符串。
    /// 日志中的 14 被替换后仅影响显示文字（"超出上限 2147483647"），不影响功能。
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

    // ═══════════════════════════════════════════════
    //  SetEnabled — Prefix（跳过原方法）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Prefix: 跳过 SkinPackManager.SetEnabled 原方法。
    /// 原方法会检查 GetEnabledCount() &gt;= 14 并拒绝启用。
    /// 我们直接修改 _preferences 字典并返回 true。
    /// </summary>
    private static bool SetEnabled_Prefix(string packId, bool enabled, ref bool __result)
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
            Log.Warn($"[{ModId}] SetEnabled Prefix failed, falling through to original: {ex.Message}");
        }

        // 如果反射失败，走原方法（带限制）
        // __result 未设置，由原方法决定
        return true;
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    /// <summary>判断 IL 指令是否为常量 14（短格式或长格式）</summary>
    private static bool IsConstant14(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 14)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && i == 14);
    }

    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}
