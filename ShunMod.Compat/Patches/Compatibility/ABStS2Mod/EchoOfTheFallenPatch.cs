using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.ABStS2Mod;

/// <summary>
///     ABStS2Mod (噬魂模组) 兼容 — EchoOfTheFallen 遗物 40% 概率 -> 100%。
///     Transpiler 将 ChancePercent 常量 40 替换为 100，
///     使 `num >= 40` 变成 `num >= 100`（NextInt(0,100) 返回 0-99，永远不满足）。
///     纯 IL 替换，不耦合原方法内部逻辑。
/// </summary>
public static class EchoOfTheFallenPatch
{
    private const string ModId = ModEntry.ModId;
    private const string TargetNs = "ABStS2Mod.Relics";
    private const string TargetType = "EchoOfTheFallen";
    private const string TargetMethod = "TryModifyCardRewardOptions";

    public static void Apply(Harmony harmony)
    {
        var targetType = CompatibilityPatchUtil.FindType(TargetNs, TargetType);
        if (targetType == null)
        {
            Log.Info($"[{ModId}] {TargetType} not detected, skipping patch");
            return;
        }

        var method = AccessTools.Method(targetType, TargetMethod);
        if (method == null)
        {
            Log.Warn($"[{ModId}] {TargetType}.{TargetMethod} not found!");
            return;
        }

        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(EchoOfTheFallenPatch), nameof(Transpiler)));
        Log.Info($"[{ModId}] {TargetType}.{TargetMethod}: chance 40% -> 100%");
    }

    /// <summary>
    ///     将方法中所有 `ldc.i4.s 40` / `ldc.i4 40` 替换为 `ldc.i4 100`，
    ///     即把 ChancePercent 从 40 改为 100。
    /// </summary>
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (IsConstant40(inst))
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = 100;
            }

            yield return inst;
        }
    }

    private static bool IsConstant40(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte s && s == 40)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && i == 40);
    }
}