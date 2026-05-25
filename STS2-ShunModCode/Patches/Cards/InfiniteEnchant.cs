using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     无限附魔系统 — 通过 RepeatableCompositeEnchantment 包装器实现多附魔。
///     Patch 1: CanEnchant 始终返回 true（任何卡牌都可被附魔）
///     Patch 2: 拦截 EnchantInternal，已有附魔时路由到复合包装器
/// </summary>
/// Patch 1 — CanEnchant 始终返回 true
[HarmonyPatch]
public static class InfiniteEnchant_CanEnchant
{
    private static MethodBase? TargetMethod()
    {
        var method = AccessTools.Method(typeof(EnchantmentModel), "CanEnchant", [typeof(CardModel)]);
        if (method != null)
        {
            ShunLogger.Info("无限附魔/CanEnchant", "匹配 CanEnchant(CardModel)");
            return method;
        }

        ShunLogger.Warn("无限附魔/CanEnchant", "未找到 CanEnchant(CardModel)，补丁跳过");
        return null;
    }

    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (!__result) __result = true;
    }
}

/// Patch 2 — EnchantInternal 拦截，路由到复合附魔
[HarmonyPatch(typeof(CardModel), nameof(CardModel.EnchantInternal))]
public static class InfiniteEnchant_EnchantInternal
{
    private static readonly PropertyInfo? EnchantmentProp =
        AccessTools.Property(typeof(CardModel), "Enchantment");

    [HarmonyPrefix]
    private static bool Prefix(CardModel __instance, EnchantmentModel enchantment, decimal amount)
    {
        var existing = __instance.Enchantment;

        // 无附魔 → 走原版
        if (existing == null) return true;

        try
        {
            // 已有复合附魔 → 直接叠加
            if (existing is RepeatableCompositeEnchantment composite)
            {
                composite.AddOrStack(enchantment, amount);
                ShunLogger.Debug("无限附魔",
                    $"复合叠加: {enchantment.GetType().Name} → {composite.InnerEnchantments.Count}种");
                return false;
            }

            // 已有原版附魔 → 升级为复合包装器
            ShunLogger.Info("无限附魔",
                $"升级为复合: {existing.GetType().Name} + {enchantment.GetType().Name}");

            var wrapper = new RepeatableCompositeEnchantment();

            // 绑定到卡牌（先设 card.Enchantment 再 ApplyInternal）
            EnchantmentProp?.SetValue(__instance, wrapper);
            wrapper.ApplyInternal(__instance, 1);

            // 导入旧附魔
            wrapper.ImportExisting(existing);

            // 添加新附魔
            wrapper.AddOrStack(enchantment, amount);

            // 统一应用所有内部附魔效果
            wrapper.ModifyCard();
            __instance.FinalizeUpgradeInternal();
            __instance.DynamicVars.RecalculateForUpgradeOrEnchant();

            return false;
        }
        catch (Exception ex)
        {
            ShunLogger.Error("无限附魔/EnchantInternal", ex);
            return true; // 炸了走原版，别崩游戏
        }
    }
}