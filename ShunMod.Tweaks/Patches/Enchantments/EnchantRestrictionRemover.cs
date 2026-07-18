using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Enchantments;

/// <summary>
///     运行时扫描所有 EnchantmentModel 子类，自动 Patch 所有 override 了
///     CanEnchant(CardModel) / CanEnchantCardType(CardType) 的附魔，
///     解除其限制，让任何卡牌都能接受任何附魔。
///     在 Harmony.PatchAll() 之后调用。
/// </summary>
internal static class EnchantRestrictionRemover
{
    private const string ModId = "ShunMod_Tweaks";

    public static void ApplyAll(Harmony harmony)
    {
        var enchantmentType = typeof(EnchantmentModel);
        var canEnchant = enchantmentType.GetMethod(
            nameof(EnchantmentModel.CanEnchant),
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [typeof(CardModel)],
            null);

        var canEnchantCardType = enchantmentType.GetMethod(
            nameof(EnchantmentModel.CanEnchantCardType),
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [typeof(CardType)],
            null);

        if (canEnchant == null && canEnchantCardType == null)
        {
            Log.Warn($"[{ModId}] EnchantRestrictionRemover: 找不到基类方法，跳过");
            return;
        }

        var patched = new List<string>();

        // 扫描所有已加载程序集中的类型
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!enchantmentType.IsAssignableFrom(type) || type == enchantmentType || type.IsAbstract)
                        continue;

                    // ── CanEnchant(CardModel) override ──
                    if (canEnchant != null)
                    {
                        var method = type.GetMethod(
                            nameof(EnchantmentModel.CanEnchant),
                            BindingFlags.Public | BindingFlags.Instance,
                            null,
                            [typeof(CardModel)],
                            null);

                        if (method != null && method.DeclaringType == type && method != canEnchant)
                        {
                            var postfix = new HarmonyMethod(typeof(EnchantRestrictionRemover),
                                nameof(ForceTruePostfix));
                            harmony.Patch(method, postfix: postfix);
                            patched.Add($"{type.Name}.CanEnchant");
                        }
                    }

                    // ── CanEnchantCardType(CardType) override ──
                    if (canEnchantCardType != null)
                    {
                        var method = type.GetMethod(
                            nameof(EnchantmentModel.CanEnchantCardType),
                            BindingFlags.Public | BindingFlags.Instance,
                            null,
                            [typeof(CardType)],
                            null);

                        if (method != null && method.DeclaringType == type && method != canEnchantCardType)
                        {
                            var prefix = new HarmonyMethod(typeof(EnchantRestrictionRemover),
                                nameof(SkipAndReturnTrue));
                            harmony.Patch(method, prefix: prefix);
                            patched.Add($"{type.Name}.CanEnchantCardType");
                        }
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // 跳过无法加载类型的程序集
            }
        }

        if (patched.Count > 0)
            Log.Info($"[{ModId}] EnchantRestrictionRemover: 已 Patch {patched.Count} 个方法：\n  {string.Join("\n  ", patched)}");
        else
            Log.Info($"[{ModId}] EnchantRestrictionRemover: 未发现需要 Patch 的附魔限制方法");
    }

    /// <summary>
    ///     CanEnchant Postfix：强制返回 true（保留原方法执行的副作用，但覆盖返回值）。
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static void ForceTruePostfix(ref bool __result)
    {
        __result = true;
    }

    /// <summary>
    ///     CanEnchantCardType Prefix：跳过原方法，直接返回 true。
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    private static bool SkipAndReturnTrue(ref bool __result)
    {
        __result = true;
        return false;
    }
}