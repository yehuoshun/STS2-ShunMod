using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 无限升级 — IsUpgradable 永远返回 true，卡牌可以反复升级。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
public static class InfiniteUpgradePatch
{
    static void Postfix(ref bool __result)
    {
        __result = true;
    }
}
