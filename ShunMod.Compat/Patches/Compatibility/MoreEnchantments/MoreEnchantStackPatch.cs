using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.MoreEnchantments;

/// <summary>
/// MoreEnchant 附魔模组兼容补丁 - 解除单卡附魔叠加上限。
///
/// 原版 MoreEnchantStack.CanApply 有两层检查：
///   1. canonical.CanEnchant(card) - 基础附魔兼容性（保留）
///   2. AllEnchantments(card).Count + MoreEnchantSacrifice.Count(card) &lt; GetMaxEnchantments(card)
///      - 按稀有度限制叠加上限（基础/普通=3，罕见=4，稀有=5，远古=6）
///
/// 本补丁移除第二层限制，允许任意数量附魔叠加到同一张牌。
/// </summary>
internal static class MoreEnchantStackPatch
{
    private const string ModId = ModEntry.ModId;
    private const string TargetNs = "MoreEnchantmentsMod";
    private const string TargetType = "MoreEnchantStack";

    private static readonly AppliedFlag Applied = new();

    private sealed class AppliedFlag
    {
        public bool Value;
    }

    public static void Apply(Harmony harmony)
    {
        var targetType = CompatibilityPatchUtil.FindType(TargetNs, TargetType);
        if (targetType != null)
        {
            ApplyPatch(harmony, targetType);
            return;
        }

        Log.Info($"[{ModId}] MoreEnchant not yet loaded, subscribing to AssemblyLoad...");
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        return;

        void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            if (Applied.Value) return;
            if (CompatibilityPatchUtil.FindType(TargetNs, TargetType) is { } t)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                ApplyPatch(harmony, t);
            }
        }
    }

    private static void ApplyPatch(Harmony harmony, Type targetType)
    {
        if (Interlocked.CompareExchange(ref Applied.Value, true, false)) return;

        Log.Info($"[{ModId}] MoreEnchant patch: applying to {targetType.FullName}");

        // CanApply(CardModel, EnchantmentModel) -> bool
        var method = AccessTools.Method(targetType, "CanApply", new[]
        {
            typeof(CardModel),
            typeof(EnchantmentModel)
        });

        if (method == null)
        {
            Log.Warn($"[{ModId}] MoreEnchant patch: CanApply method not found!");
            return;
        }

        harmony.Patch(method, prefix: new HarmonyMethod(typeof(MoreEnchantStackPatch), nameof(CanApply_Prefix)));
        Log.Info($"[{ModId}] MoreEnchant patch: CanApply (Prefix, unrestricted)");
    }

    /// <summary>
    /// Prefix：移除叠加上限检查，只保留基础 CanEnchant 兼容性。
    /// 原方法返回 false 当叠加数 >= 上限时，我们改为返回 true 让原方法通过。
    /// </summary>
    private static bool CanApply_Prefix(
        CardModel card,
        EnchantmentModel canonical,
        ref bool __result)
    {
        // 保留基础兼容性检查
        if (!canonical.CanEnchant(card))
        {
            __result = false;
            return false; // 跳过原方法
        }

        // 移除上限限制，直接允许附魔
        __result = true;
        return false; // 跳过原方法
    }
}