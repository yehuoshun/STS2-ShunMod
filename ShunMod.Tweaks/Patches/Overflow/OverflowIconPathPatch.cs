using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用

/// <summary>
///     超限系列 Power 图标路径修正。
///     PowerModel.PackedIconPath / BigIconPath 非 virtual 且硬编码无前缀
///     res://images/...，而 PckPacker 产物带 ShunMod.Tweaks 前缀 →
///     按原路径永远找不到图标。此处用 Harmony 模拟 override（同 Shun 模块
///     Card/Relic 的做法）：仅对超限两个 Power 返回带前缀路径，其余 Power
///     原样放行，游戏本体图标不受影响。
/// </summary>
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.PackedIconPath), MethodType.Getter)]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class OverflowIconPathPatch
{
    private const string IconPrefix = "res://ShunMod.Tweaks/images";

    private static bool IsOverflow(PowerModel power)
    {
        var entry = power.Id.Entry;
        return entry == "OVERFLOW_POWER" || entry == "OVERFLOW_LIFE_POWER";
    }

    [HarmonyPrefix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (!IsOverflow(__instance))
        {
            return true;
        }

        var id = __instance.Id.Entry.ToLowerInvariant();
        __result = $"{IconPrefix}/atlases/power_atlas.sprites/{id}.tres";
        return false;
    }
}

/// <summary>
///     超限大图路径（战斗外/大图标场景）：BigIconPath 是 private getter，
///     同样硬编码无前缀，一并修正。
/// </summary>
[HarmonyPatch(typeof(PowerModel), "get_BigIconPath")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class OverflowBigIconPathPatch
{
    private const string IconPrefix = "res://ShunMod.Tweaks/images";

    private static bool IsOverflow(PowerModel power)
    {
        var entry = power.Id.Entry;
        return entry == "OVERFLOW_POWER" || entry == "OVERFLOW_LIFE_POWER";
    }

    [HarmonyPrefix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (!IsOverflow(__instance))
        {
            return true;
        }

        var id = __instance.Id.Entry.ToLowerInvariant();
        __result = $"{IconPrefix}/powers/{id}.png";
        return false;
    }
}
