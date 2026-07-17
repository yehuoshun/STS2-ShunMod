using HarmonyLib;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14 → 无限）。
/// 实现见 <see cref="LimitPatchHelper"/>，此类仅提供配置参数。
/// Transpiler 替换 14/140 为 int.MaxValue，比较和日志字符串同时覆盖。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = ModEntry.ModId;
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    private static readonly LimitPatchHelper.AppliedFlag Applied = new();

    public static void Apply(Harmony harmony) =>
        LimitPatchHelper.Apply(harmony, ModId, TargetNs, TargetType, "Shadow verse SkinLimit",
            Applied, typeof(ShadowverseSkinLimitPatch),
            nameof(ScanInstalledPacks_Transpiler), nameof(SetEnabled_Transpiler));

    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant14);

    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) =>
        LimitPatchHelper.ReplaceLimitConstant(instructions, LimitPatchHelper.IsConstant14);
}