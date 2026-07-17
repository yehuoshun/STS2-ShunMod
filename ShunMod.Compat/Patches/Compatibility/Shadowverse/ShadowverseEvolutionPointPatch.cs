using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 进化点系统全解除。
/// 进化流程：Initialize → TryUseEvolvePoint → MarkEvolveUsedThisTurn → GetEvolveUsedThisTurn
/// Patch 策略：Initialize 改初始值 1（UI 展示用），TryUse 跳过（不消耗），Mark 跳过（解回合限制）。
/// 不 patch GetEvolveUsedThisTurn，保留给其他卡牌做进化检测。
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

        PatchMethod(harmony, evoType, "Initialize",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Initialize_Prefix));

        PatchMethod(harmony, evoType, "TryUseEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));
        PatchMethod(harmony, evoType, "TryUseSuperEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));

        // 解回合限制，不 patch GetEvolveUsedThisTurn
        PatchMethod(harmony, evoType, "MarkEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));
        PatchMethod(harmony, evoType, "MarkSuperEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));
    }

    [SuppressMessage("ReSharper", "RedundantAssignment")]
    private static void Initialize_Prefix(ref int evolvePoints, ref int superEvolvePoints)
    {
        evolvePoints = 1;
        superEvolvePoints = 1;
    }

    [SuppressMessage("ReSharper", "RedundantAssignment")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static bool TryUse_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    private static bool Skip_Prefix() => false;

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