using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.MoreEnchantments;

/// <summary>
/// MoreEnchant 附魔模组兼容补丁 - 解除 UI 层附魔上限检查。
///
/// MoreEnchantStackPatch 已经移除了 <c>MoreEnchantStack.CanApply</c> 的叠加上限，
/// 但 MoreEnchantmentsMod 的 <c>MoreEnchantPayCheckPatch.OnCardClicked</c> Prefix 中
/// 还有一层独立的 <c>num &gt;= maxEnchantments</c> 检查，达到上限时返回 <c>false</c> 阻止附魔。
///
/// 本补丁将 <c>MoreEnchantStack.GetMaxEnchantments</c> 的返回值改为 <c>int.MaxValue</c>，
/// 使 UI 层的上限检查永远通过，解除第二层限制。
/// </summary>
internal static class MoreEnchantPayCheckPatch
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
            if (CompatibilityPatchUtil.FindType(TargetNs, TargetType) is not { } t) return;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            ApplyPatch(harmony, t);
        }
    }

    private static void ApplyPatch(Harmony harmony, Type targetType)
    {
        if (Interlocked.CompareExchange(ref Applied.Value, true, false)) return;

        Log.Info($"[{ModId}] MoreEnchant pay-check patch: applying to {targetType.FullName}");

        // GetMaxEnchantments(CardModel) -> int
        var method = AccessTools.Method(targetType, "GetMaxEnchantments", [typeof(CardModel)]);

        if (method == null)
        {
            Log.Warn($"[{ModId}] MoreEnchant pay-check patch: GetMaxEnchantments method not found!");
            return;
        }

        harmony.Patch(method, postfix: new HarmonyMethod(typeof(MoreEnchantPayCheckPatch), nameof(GetMaxEnchantments_Postfix)));
        Log.Info($"[{ModId}] MoreEnchant pay-check patch: GetMaxEnchantments (Postfix, always return int.MaxValue)");
    }

    /// <summary>
    /// Postfix：将 GetMaxEnchantments 的返回值改为 int.MaxValue，
    /// 使 MoreEnchantPayCheckPatch.OnCardClicked Prefix 中的
    /// <c>num &gt;= maxEnchantments</c> 检查永远无法触发。
    /// </summary>
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static void GetMaxEnchantments_Postfix(ref int __result)
    {
        __result = int.MaxValue;
    }
}