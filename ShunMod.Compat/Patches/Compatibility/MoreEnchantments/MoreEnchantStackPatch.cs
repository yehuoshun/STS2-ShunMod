using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.MoreEnchantments;

/// <summary>
///     MoreEnchant 附魔模组兼容补丁 - 解除单卡附魔叠加上限。
///     原版 CanApply 有两层检查：CanEnchant（保留）+ 稀有度上限（移除）。
/// </summary>
internal static class MoreEnchantStackPatch
{
    private const string ModId = ModEntry.ModId;
    private const string TargetNs = "MoreEnchantmentsMod";
    private const string TargetType = "MoreEnchantStack";

    private static readonly AppliedFlag Applied = new();

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
            if (CompatibilityPatchUtil.FindType(TargetNs, TargetType) is not { } t) return;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            ApplyPatch(harmony, t);
        }
    }

    private static void ApplyPatch(Harmony harmony, Type targetType)
    {
        if (Interlocked.CompareExchange(ref Applied.Value, true, false)) return;

        Log.Info($"[{ModId}] MoreEnchant patch: applying to {targetType.FullName}");

        var method = AccessTools.Method(targetType, "CanApply", [typeof(CardModel), typeof(EnchantmentModel)]);
        if (method == null)
        {
            Log.Warn($"[{ModId}] MoreEnchant patch: CanApply method not found!");
            return;
        }

        harmony.Patch(method, new HarmonyMethod(typeof(MoreEnchantStackPatch), nameof(CanApply_Prefix)));
        Log.Info($"[{ModId}] MoreEnchant patch: CanApply (Prefix, unrestricted)");
    }

    [SuppressMessage("ReSharper", "RedundantAssignment")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static bool CanApply_Prefix(
        CardModel card,
        EnchantmentModel canonical,
        ref bool __result)
    {
        if (!canonical.CanEnchant(card))
        {
            __result = false;
            return false;
        }

        __result = true;
        return false;
    }

    private sealed class AppliedFlag
    {
        public bool Value;
    }
}