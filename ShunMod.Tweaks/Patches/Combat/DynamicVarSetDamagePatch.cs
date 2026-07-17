using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShunMod.Tweaks.Patches.Combat;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
/// <summary>
///     修复 <c>DynamicVarSet.Damage</c> getter 在缺失 'Damage' key 时
///     抛 <c>KeyNotFoundException</c> 导致游戏崩溃的问题。
///     第三方模组（如 MoreEnchantmentsMod 的 Prismatic 附魔）直接调用
///     <c>.Damage</c> 属性（而非 <c>TryGetValue</c>），但 StS2 有 20 张卡
///     使用 <c>CalculatedDamage</c> key 而非 <c>Damage</c> key
///     （如 BODY_SLAM、MIND_BLAST、PERFECTED_STRIKE、Osty 的 UNLEASH 等），
///     原版索引器 <c>this["Damage"]</c> 在 key 不存在时抛异常。
///     本补丁改为：
///     1. 优先读 <c>Damage</c> key
///     2. 不存在时兜底 <c>CalculatedDamage</c> key
///     3. 两者都不存在时返回 <c>null</c>（而非抛异常）
/// </summary>
[HarmonyPatch(typeof(DynamicVarSet), "get_Damage")]
public static class DynamicVarSetDamagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(DynamicVarSet __instance, ref DynamicVar __result)
    {
        if (__instance.TryGetValue("Damage", out var dv))
        {
            __result = dv;
            return false;
        }

        // 兜底：StS2 有 20 张卡用 CalculatedDamage 替代 Damage
        if (__instance.TryGetValue("CalculatedDamage", out var cd))
        {
            __result = cd;
            return false;
        }

        // 两者都不存在，返回 null 避免 KeyNotFoundException 崩溃
        __result = null!;
        return false;
    }
}