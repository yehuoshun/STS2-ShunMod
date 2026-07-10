using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 进化点系统全解除。
///
/// 基于进化点源码重构后的精确 patch：
///   进化点数     → Prefix 拦截 Initialize 参数，设为 1（仅用于 UI 展示）
///   进化不消耗    → Prefix 跳过 TryUseEvolvePoint / TryUseSuperEvolvePoint
///   回合限制解除  → Postfix 让 GetEvolveUsedThisTurn / GetSuperEvolveUsedThisTurn 永远返回 false
/// </summary>
public static class ShadowverseEvolutionPointPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "EvolutionPointManager";

    public static void Apply(Harmony harmony)
    {
        var evoType = CompatibilityPatchUtil.FindPatchType(ModId, TargetNs, TargetType);
        if (evoType == null) return;

        // ── Patch 1: Initialize ──
        // 默认参数 (evolve=2, superEvolve=2) 由编译器在调用点内联，
        // Prefix 用 ref int 截获改为 1，UI 显示"有进化点"即可。
        PatchMethod(harmony, evoType, "Initialize",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Initialize_Prefix));

        // ── Patch 2: TryUseEvolvePoint ──
        // 跳过原方法，进化不消耗点数。
        PatchMethod(harmony, evoType, "TryUseEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));

        // ── Patch 3: TryUseSuperEvolvePoint ──
        PatchMethod(harmony, evoType, "TryUseSuperEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));

        // ── Patch 4: GetEvolveUsedThisTurn ──
        // 永远返回 false，解除"一回合只能进化一次"限制。
        PatchMethod(harmony, evoType, "GetEvolveUsedThisTurn",
            postfixType: typeof(ShadowverseEvolutionPointPatch),
            postfixName: nameof(ReturnFalse_Postfix));

        // ── Patch 5: GetSuperEvolveUsedThisTurn ──
        PatchMethod(harmony, evoType, "GetSuperEvolveUsedThisTurn",
            postfixType: typeof(ShadowverseEvolutionPointPatch),
            postfixName: nameof(ReturnFalse_Postfix));
    }

    // ═══════════════════════════════════════════════
    //  Harmony 方法
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 让 Initialize 给玩家 1 点进化点（只影响 UI 显示，实际进化不消耗）。
    /// 编译器在调用点内联了默认值，ref 参数可以截获。
    /// </summary>
    private static void Initialize_Prefix(ref int evolvePoints, ref int superEvolvePoints)
    {
        evolvePoints = 1;
        superEvolvePoints = 1;
    }

    /// <summary>
    /// TryUseEvolvePoint / TryUseSuperEvolvePoint — 跳过原方法，进化始终成功。
    /// Prefix 返回 false 时跳过原方法体，__result 设为 true。
    /// </summary>
    private static bool TryUse_Prefix(ref bool __result)
    {
        __result = true;
        return false; // 跳过原方法 → 进化点不递减
    }

    /// <summary>
    /// GetEvolveUsedThisTurn / GetSuperEvolveUsedThisTurn — 永远返回 false。
    /// 让进化判定认为"本回合未进化过"，解除回合限制。
    /// </summary>
    private static void ReturnFalse_Postfix(ref bool __result)
    {
        __result = false;
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 安全地给 Harmony 加补丁，方法不存在时只打日志不抛异常。
    /// </summary>
    private static void PatchMethod(Harmony harmony, Type type, string methodName,
        Type? prefixType = null, string? prefixName = null,
        Type? postfixType = null, string? postfixName = null)
    {
        var method = AccessTools.Method(type, methodName);
        if (method == null)
        {
            Log.Warn($"[{ModId}] EvolutionPoint: {methodName} method not found!");
            return;
        }

        harmony.Patch(method,
            prefix: prefixType != null ? new HarmonyMethod(prefixType, prefixName!) : null,
            postfix: postfixType != null ? new HarmonyMethod(postfixType, postfixName!) : null);

        Log.Info($"[{ModId}] EvolutionPoint: {methodName} patched");
    }
}