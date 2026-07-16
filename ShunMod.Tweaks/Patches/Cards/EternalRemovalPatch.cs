using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Cards;

// ═══════════════════════════════════════════════════════════════════════════════
// 取消永恒卡牌的移除/变化限制
//   - IsRemovable：始终返回 true（原版：有 Eternal 关键字 → false）
//   - IsTransformable：始终返回 true（原版：有 Eternal 且 in Deck → false）
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __result 约定

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsRemovable), MethodType.Getter)]
public static class EternalRemovablePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        __result = true;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsTransformable), MethodType.Getter)]
public static class EternalTransformablePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        __result = true;
    }
}