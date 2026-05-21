using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     无限升级 — 极简版。
///     直接 Patch MaxUpgradeLevel getter 返回 int.MaxValue。
///     不用 TargetMethods、不处理 FromSerializable 上下文（太复杂且容易炸）。
///     副作用：读档后升级等级会正确加载但 CurrentUpgradeLevel 的 setter 检查会通过。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel), MethodType.Getter)]
public static class InfiniteUpgrade
{
    /// <summary>
    ///     MaxUpgradeLevel > 0 的卡牌改为无上限。
    ///     = 0 的（诅咒/状态牌）不动。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(ref int __result)
    {
        if (__result > 0)
            __result = int.MaxValue;
    }
}