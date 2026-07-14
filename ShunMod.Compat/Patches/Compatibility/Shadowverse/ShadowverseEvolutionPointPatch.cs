using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 进化点系统全解除。
///
/// Patch 策略：
///   Initialize → Prefix 改初始值 1（UI 展示用，进化不消耗）
///   TryUseEvolvePoint / TryUseSuperEvolvePoint → Prefix 跳过（不消耗点数）
///   MarkEvolveUsedThisTurn / MarkSuperEvolveUsedThisTurn → Prefix 跳过（解回合限制）
///
/// 不 patch GetEvolveUsedThisTurn：保留给其他卡牌和机制做进化检测。
///
/// 反编译确认的源码结构：
///   _points = Dictionary{Player, (int evolve, int superEvolve)}
///   _evolveUsedThisTurn / _superEvolveUsedThisTurn = HashSet{Player} ← 进化标记
///
/// 进化流程：
///   Initialize(player, 2, 2) → TryUseEvolvePoint → MarkEvolveUsedThisTurn → GetEvolveUsedThisTurn
///   Patch 跳过：         → 跳过（不消耗）     → 跳过（不标记）     → 自然返回 false
/// </summary>
public static class ShadowverseEvolutionPointPatch
{
    private const string ModId = ModEntry.ModId;
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "EvolutionPointManager";

    public static void Apply(Harmony harmony)
    {
        var evoType = CompatibilityPatchUtil.FindPatchType(ModId, TargetNs, TargetType);
        if (evoType == null) return;

        // Initialize 默认参数 (2,2) 由编译器在调用点内联，Prefix 用 ref 截获改为 1
        PatchMethod(harmony, evoType, "Initialize",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Initialize_Prefix));

        // TryUseEvolvePoint / TryUseSuperEvolvePoint — 跳过原方法，进化不消耗
        PatchMethod(harmony, evoType, "TryUseEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));
        PatchMethod(harmony, evoType, "TryUseSuperEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));

        // MarkEvolveUsedThisTurn / MarkSuperEvolveUsedThisTurn — 跳过标记，解回合限制
        // 不 patch GetEvolveUsedThisTurn，保留给其他卡牌做进化检测
        PatchMethod(harmony, evoType, "MarkEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));
        PatchMethod(harmony, evoType, "MarkSuperEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));
    }

    // ═══════════════════════════════════════════════
    //  Harmony 方法
    // ═══════════════════════════════════════════════

    // ReSharper disable All
    private static void Initialize_Prefix(ref int evolvePoints, ref int superEvolvePoints)
    {
        evolvePoints = 1;
        superEvolvePoints = 1;
    }
    // ReSharper restore All

    // ReSharper disable All
    private static bool TryUse_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
    // ReSharper restore All

    private static bool Skip_Prefix()
    {
        return false;
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    /// <summary>安全地给 Harmony 加补丁，方法不存在时只打日志不抛异常。</summary>
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