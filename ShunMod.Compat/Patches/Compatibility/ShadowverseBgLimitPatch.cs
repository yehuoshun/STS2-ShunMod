using System.Reflection.Emit;
using HarmonyLib;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除背景包启用数量限制（7 → 无限）。
/// 实现全在 <see cref="LimitPatchHelper"/> 共享模式中，此类仅提供配置参数。
///
/// 反编译确认的限制点：
///   ScanInstalledPacks（启动加载）：
///     bool flag6 = flag5 && num >= 7;
///     if (flag6) { flag5 = false; Log.Warn(...AppendFormatted(7)...); }
///   SetEnabled（运行时 UI 开关）：
///     bool flag3 = BgPackManager.GetEnabledCount() >= 7;
///     if (flag3) { Log.Warn(...AppendFormatted(7)...); return false; }
/// </summary>
public static class ShadowverseBgLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts.UI";
    private const string TargetType = "BgPackManager";

    private static bool _applied;

    public static void Apply(Harmony harmony) =>
        LimitPatchHelper.Apply(harmony, ModId, TargetNs, TargetType, "Shadow verse BgLimit",
            ref _applied, typeof(ShadowverseBgLimitPatch),
            nameof(ScanInstalledPacks_Transpiler), nameof(SetEnabled_Transpiler));

    // ═══════════════════════════════════════════════
    //  Transpiler
    // ═══════════════════════════════════════════════

    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant7);

    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant7);
}