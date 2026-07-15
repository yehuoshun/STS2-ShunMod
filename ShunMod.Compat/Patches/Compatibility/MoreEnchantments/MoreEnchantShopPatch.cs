using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.MoreEnchantments;

/// <summary>
/// MoreEnchant 附魔模组兼容补丁 — 解除 AddTier 的附魔限制。
///
/// 原版 AddTier 有两层限制：
///   1. 调用 GetShopCandidates 获取候选，该方法有 IsShopTier/CanApply/PlayableEnchantmentTypes 三重过滤
///   2. count 参数 + inventory.Count >= 6 上限，限制每次加入的附魔数量
///
/// 本补丁移除所有限制，改为直接通过 MoreEnchantRegistry.PlayableEnchantmentTypes 获取所有附魔类型，
/// 不受 count/inventory 上限限制，不受 CanApply 过滤影响（保留 IsShopTier 排除 Eternal+ 的过滤）。
/// </summary>
internal static class MoreEnchantShopPatch
{
    private const string ModId = ModEntry.ModId;
    private const string TargetNs = "MoreEnchantmentsMod";
    private const string TargetType = "MoreEnchantFakeMerchantShop";

    private static readonly AppliedFlag Applied = new();

    // 反射缓存 — 只在 ApplyPatch 成功时初始化一次
    private static Type? _moreEnchantmentModelType;
    private static object? _playableEnchantmentTypes;
    private static Type? _enchantmentTierType;
    private static object? _eternalValue;

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

        var tierEnum = FindTierEnum(targetType.Assembly);
        var rngType = FindRngType();

        if (tierEnum == null || rngType == null)
        {
            Log.Warn($"[{ModId}] MoreEnchant patch: tierEnum or rngType not found, skipping");
            return;
        }

        var paramTypes = new[]
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

        // 初始化反射缓存
        InitReflectionCache(targetType.Assembly, tierEnum);

        harmony.Patch(method, prefix: new HarmonyMethod(typeof(MoreEnchantShopPatch), nameof(AddTier_Prefix)));
        Log.Info($"[{ModId}] MoreEnchant patch: AddTier (Prefix, unrestricted)");
    }

    private static void InitReflectionCache(Assembly targetAssembly, Type tierEnum)
    {
        try
        {
            // MoreEnchantmentModel — 继承自 EnchantmentModel，有 Tier 属性
            _moreEnchantmentModelType = targetAssembly.GetType("MoreEnchantmentsMod.MoreEnchantmentModel");

            // MoreEnchantRegistry.PlayableEnchantmentTypes — 静态字段/属性，List<Type>
            var registryType = targetAssembly.GetType("MoreEnchantmentsMod.MoreEnchantRegistry");
            if (registryType != null)
            {
                var field = registryType.GetField("PlayableEnchantmentTypes",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    _playableEnchantmentTypes = field.GetValue(null);
                else
                {
                    var prop = registryType.GetProperty("PlayableEnchantmentTypes",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                        _playableEnchantmentTypes = prop.GetValue(null);
                }
            }

            // EnchantmentTier.Eternal — 用于跳过非商店 tier
            _enchantmentTierType = tierEnum;
            _eternalValue = Enum.Parse(tierEnum, "Eternal");
        }
        catch (Exception ex)
        {
            Log.Warn($"[{ModId}] MoreEnchant patch: InitReflectionCache failed: {ex.Message}");
        }
    }

    private static bool AddTier_Prefix(
        List<EnchantmentModel> inventory,
        HashSet<string> used)
    {
        try
        {
            if (_playableEnchantmentTypes == null || _moreEnchantmentModelType == null || _enchantmentTierType == null)
            {
                Log.Warn($"[{ModId}] MoreEnchant patch: reflection cache not initialized, skipping");
                return false;
            }

            // GetById 的反射调用缓存
            var modelDbGetId = typeof(ModelDb).GetMethod("GetId", BindingFlags.Static | BindingFlags.Public,
                new[] { typeof(Type) });
            var modelDbGetById = typeof(ModelDb).GetMethod("GetById", BindingFlags.Static | BindingFlags.Public)
                ?.MakeGenericMethod(typeof(EnchantmentModel));

            if (modelDbGetId == null || modelDbGetById == null)
            {
                Log.Warn($"[{ModId}] MoreEnchant patch: ModelDb reflection failed");
                return false;
            }

            // 遍历 PlayableEnchantmentTypes
            var types = _playableEnchantmentTypes as System.Collections.IEnumerable;
            if (types == null) return false;

            foreach (var typeObj in types)
            {
                var enchantType = (Type)typeObj;
                var id = modelDbGetId.Invoke(null, new[] { enchantType });
                var enchantment = (EnchantmentModel?)modelDbGetById.Invoke(null, new[] { id });
                if (enchantment == null) continue;

                // 跳过 Eternal+ 的非商店 tier（保留原版 IsShopTier 过滤）
                if (IsEternalOrAbove(enchantment)) continue;

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

    /// <summary>
    /// 判断附魔是否为 Eternal 及以上 tier（非商店 tier，原版 IsShopTier 的简化版）。
    /// </summary>
    private static bool IsEternalOrAbove(EnchantmentModel enchantment)
    {
        if (_moreEnchantmentModelType == null || _enchantmentTierType == null || _eternalValue == null)
            return false;

        // 不是 MoreEnchantmentModel 的跳过（不拦截）
        if (!_moreEnchantmentModelType.IsInstanceOfType(enchantment))
            return false;

        try
        {
            var tierProp = _moreEnchantmentModelType.GetProperty("Tier");
            if (tierProp == null) return false;

            var tier = tierProp.GetValue(enchantment);
            if (tier == null) return false;

            // tier >= Eternal 则跳过
            var eternal = (int)_eternalValue;
            var current = (int)tier;
            return current >= eternal;
        }
        catch
        {
            return false;
        }
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

    private static Type? FindRngType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("MegaCrit.Sts2.Core.Random.Rng");
            if (t != null) return t;
        }
        return null;
    }
}
