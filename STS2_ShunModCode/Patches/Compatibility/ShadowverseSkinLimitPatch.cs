using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（旧版 14 / 新版 140 → 无限）。
///
/// 反编译确认限制在两处：
///   1. ScanInstalledPacks（启动时扫描）：num &gt;= 140 时强制禁用后续皮肤
///   2. SetEnabled（运行时 UI 开关）：GetEnabledCount() &gt;= 140 时拒绝启用
///
/// 方案：
///   ScanInstalledPacks → Transpiler: 将常量 14 和 140 都替换为 int.MaxValue
///     （新版已改为 140，旧版为 14，两条都要匹配）
///   SetEnabled → Prefix: 跳过原方法，始终允许启用/禁用，__result=true
///     （Prefix 比 Transpiler 更可靠，不依赖 IL 布局）
///
/// 纯反射，不引用 shadowverse.dll。
/// 修复：SetEnabled 改用 Prefix 避免 Transpiler 对 GetEnabledCount() >= 14 匹配失败的风险。
/// 2026-07-06: IsConstant14(14) → IsSkinLimitConstant(14||140)，新版已升级到 140 上限。
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

        // ── Patch 1: ScanInstalledPacks Transpiler — 14/140 → int.MaxValue ──
        var scanMethod = AccessTools.Method(_skinMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: ScanInstalledPacks cap removed (14/140→unlimited)");
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
    /// Transpiler: 将 ScanInstalledPacks IL 中的皮肤上限常量（14 或 140）替换为 int.MaxValue。
    /// 覆盖：num &gt;= 14/140 的比较 + AppendFormatted&lt;int&gt;(14/140) 的日志字符串。
    /// 日志中的常量被替换后仅影响显示文字（"超出上限 2147483647"），不影响功能。
    /// 同时匹配 ldc.i4.s（短格式 ≤127）和 ldc.i4（长格式）。
    /// 新版已从 14 升到 140，两条都要处理。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (IsSkinLimitConstant(inst))
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

    /// <summary>
    /// 判断 IL 指令是否为皮肤上限常量（14 或 140）。
    /// 同时处理短格式 ldc.i4.s（14, ≤127）和长格式 ldc.i4（140, &gt;127）。
    /// 双常量匹配：旧版 14 + 新版 140，确保不同版本都生效。
    /// </summary>
    private static bool IsSkinLimitConstant(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 14)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && (i == 14 || i == 140));
    }

    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}
