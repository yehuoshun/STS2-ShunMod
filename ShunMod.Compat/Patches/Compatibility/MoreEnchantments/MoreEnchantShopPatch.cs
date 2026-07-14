using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility.MoreEnchantments;

/// <summary>
/// MoreEnchant 附魔模组兼容补丁 — 解除 AddTier 的附魔限制。
///
/// 原版 AddTier 有两层限制：
///   1. 调用 GetShopCandidates 获取候选，该方法有 IsShopTier/CanApply/PlayableEnchantmentTypes 三重过滤
///   2. count 参数 + inventory.Count >= 6 上限，限制每次加入的附魔数量
///
/// 本补丁移除所有限制，改为直接插入 ModelDb 中所有注册的 EnchantmentModel，
/// 不受 count/inventory 上限限制，不受 GetShopCandidates 过滤影响。
/// </summary>
internal static class MoreEnchantShopPatch
{
    private const string ModId = ModEntry.ModId;
    private const string TargetNs = "MoreEnchantmentsMod";
    private const string TargetType = "MoreEnchantFakeMerchantShop";

    private static readonly AppliedFlag _applied = new();

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
            if (_applied.Value) return;
            if (CompatibilityPatchUtil.FindType(TargetNs, TargetType) is { } t)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                ApplyPatch(harmony, t);
            }
        }
    }

    private static void ApplyPatch(Harmony harmony, Type targetType)
    {
        if (Interlocked.CompareExchange(ref _applied.Value, true, false)) return;

        Log.Info($"[{ModId}] MoreEnchant patch: applying to {targetType.FullName}");

        var tierEnum = FindTierEnum(targetType.Assembly);
        var rngType = FindRngType(targetType.Assembly);

        if (tierEnum == null || rngType == null)
        {
            Log.Warn($"[{ModId}] MoreEnchant patch: tierEnum or rngType not found, skipping");
            return;
        }

        var paramTypes = new Type[]
        {
            typeof(List<EnchantmentModel>),
            typeof(HashSet<string>),
            typeof(Player),
            rngType,
            tierEnum,
            typeof(int)
        };

        var method = AccessTools.Method(targetType, "AddTier", paramTypes);
        if (method == null)
        {
            Log.Warn($"[{ModId}] MoreEnchant patch: AddTier method not found!");
            return;
        }

        harmony.Patch(method, prefix: new HarmonyMethod(typeof(MoreEnchantShopPatch), nameof(AddTier_Prefix)));
        Log.Info($"[{ModId}] MoreEnchant patch: AddTier (Prefix, unrestricted)");
    }

    private static Type? FindTierEnum(Assembly targetAssembly)
    {
        var t = targetAssembly.GetType("MoreEnchantmentsMod.EnchantmentTier");
        if (t != null) return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType("MoreEnchantmentsMod.EnchantmentTier");
            if (t != null) return t;
        }
        return null;
    }

    private static Type? FindRngType(Assembly targetAssembly)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("MegaCrit.Sts2.Core.Random.Rng");
            if (t != null) return t;
        }
        return null;
    }

    private static bool AddTier_Prefix(
        List<EnchantmentModel> inventory,
        HashSet<string> used)
    {
        try
        {
            foreach (var enchantment in ModelDb.DebugEnchantments)
            {
                if (used.Add(enchantment.Id.ToString()))
                {
                    inventory.Add(enchantment);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[{ModId}] MoreEnchant patch: AddTier failed: {ex.Message}");
        }
        return false;
    }
}
