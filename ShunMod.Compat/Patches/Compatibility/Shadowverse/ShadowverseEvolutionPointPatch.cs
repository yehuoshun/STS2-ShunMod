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
///   回合限制解除  → Prefix 跳过 MarkEvolveUsedThisTurn / MarkSuperEvolveUsedThisTurn
///                   （不 patch GetEvolveUsedThisTurn，保留给其他卡和机制做进化检测）
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

        // ── Patch 4: MarkEvolveUsedThisTurn ──
        // 不 patch GetEvolveUsedThisTurn（保留给其他卡和机制做进化检测）。
        // 改为 patch 标记方法，阻止 player 被加入 HashSet。
        // 这样 GetEvolveUsedThisTurn 自然返回 false，无需覆写。
        PatchMethod(harmony, evoType, "MarkEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));

        // ── Patch 5: MarkSuperEvolveUsedThisTurn ──
        PatchMethod(harmony, evoType, "MarkSuperEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));
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
    /// MarkEvolveUsedThisTurn / MarkSuperEvolveUsedThisTurn — 跳过，不标记进化状态。
    /// 阻止 player 被加入 _evolveUsedThisTurn / _superEvolveUsedThisTurn HashSet。
    /// 这样 GetEvolveUsedThisTurn 自然返回 false，保留给其他卡牌做进化检测。
    /// </summary>
    private static bool Skip_Prefix()
    {
        return false; // 跳过原方法 → 不标记进化状态
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