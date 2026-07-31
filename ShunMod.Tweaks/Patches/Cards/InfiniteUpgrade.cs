using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShunMod.Tweaks.Patches.Cards;

// ════════════════════════════════ 主 Patch：MaxUpgradeLevel getter ════════════════════════════════

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance/__result/__exception 约定
// ReSharper disable RedundantAssignment — Harmony ref __result 直接赋值
[HarmonyPatch]
public static class InfiniteUpgradeMaxUpgrade
{
    private const int UpgradeCap = 99;

    // 缓存所有 CardModel 子类的 MaxUpgradeLevel getter（200+ 类型，只扫一次）
    // Harmony 对 TargetMethods() 只迭代一次，List<MethodInfo> 无影响
    private static readonly List<MethodInfo> TargetGetterCache = BuildTargetGetters();

    private static List<MethodInfo> TargetMethods()
    {
        return TargetGetterCache;
    }

    private static List<MethodInfo> BuildTargetGetters()
    {
        var getters = new List<MethodInfo>();

        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null) getters.Add(baseGetter);

        getters.AddRange(
            typeof(CardModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(CardModel).IsAssignableFrom(t))
                .Select(t => (type: t, getter: AccessTools.PropertyGetter(t, nameof(CardModel.MaxUpgradeLevel))))
                .Where(x => x.getter?.DeclaringType == x.type)
                .Select(x => x.getter!));

        return getters;
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref int __result)
    {
        if (InfiniteUpgradeSafety.CanUseUnlimitedGrowth(__instance, __result) && __result < UpgradeCap)
            __result = UpgradeCap;

        var serializedLevel = InfiniteUpgradeSerializationContext.Peek();
        if (serializedLevel > __result &&
            InfiniteUpgradeSafety.ShouldAllowUpgrade(__instance, __result, serializedLevel))
        {
            __result = serializedLevel;
            return;
        }

        var currentLevel = __instance.CurrentUpgradeLevel;
        if (currentLevel > __result &&
            InfiniteUpgradeSafety.ShouldAllowUpgrade(__instance, __result, currentLevel))
            __result = currentLevel;
    }
}

// ════════════════════════════════ 反序列化 Patch ════════════════════════════════

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __exception 约定
[HarmonyPatch(typeof(CardModel), "FromSerializable")]
public static class InfiniteUpgradeDeserialize
{
    private static void Prefix(SerializableCard save)
    {
        var level = InfiniteUpgradeSafety.PrepareSerializableUpgradeLevel(save);
        InfiniteUpgradeSerializationContext.Push(level);
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        InfiniteUpgradeSerializationContext.Pop();
        return __exception;
    }
}

// ════════════════════════════════ 线程安全序列化上下文 ════════════════════════════════

internal static class InfiniteUpgradeSerializationContext
{
    [ThreadStatic] private static Stack<int>? _stack;

    public static void Push(int upgradeLevel)
    {
        (_stack ??= new Stack<int>()).Push(upgradeLevel);
    }

    public static void Pop()
    {
        if (_stack is { Count: > 0 }) _stack.Pop();
    }

    public static int Peek()
    {
        return _stack is { Count: > 0 } ? _stack.Peek() : 0;
    }
}

// ════════════════════════════════ 安全检测 ════════════════════════════════════

internal static class InfiniteUpgradeSafety
{
    private static readonly HashSet<string> DrawSensitiveMethods =
        ["BeforeHandDraw", "AfterCardDrawn", "ModifyHandDraw"];

    private static readonly ConcurrentDictionary<Type, bool> DrawSensitiveCache = new();

    public static bool CanUseUnlimitedGrowth(CardModel card, int originalMaxUpgradeLevel)
    {
        if (originalMaxUpgradeLevel <= 0) return false;
        return !IsDrawSensitive(card.GetType());
    }

    public static bool ShouldAllowUpgrade(CardModel card, int originalMax, int level)
    {
        return level > originalMax && CanUseUnlimitedGrowth(card, originalMax);
    }

    public static int PrepareSerializableUpgradeLevel(SerializableCard save)
    {
        var level = Math.Max(0, save.CurrentUpgradeLevel);
        if (level == 0 || save.Id == null) return level;

        var card = ResolveCanonicalCard(save.Id);
        if (card == null) return level;

        var originalMax = Math.Max(0, card.MaxUpgradeLevel);
        if (level <= originalMax || CanUseUnlimitedGrowth(card, originalMax)) return level;

        save.CurrentUpgradeLevel = originalMax;
        return originalMax;
    }

    private static CardModel? ResolveCanonicalCard(ModelId id)
    {
        return ModelDb.GetByIdOrNull<CardModel>(id);
    }

    private static bool IsDrawSensitive(Type cardType)
    {
        return DrawSensitiveCache.GetOrAdd(cardType, ComputeDrawSensitive);
    }

    private static bool ComputeDrawSensitive(Type cardType)
    {
        return TypeHierarchy().Any(t =>
            DrawSensitiveMethods.Any(methodName =>
            {
                var method = t.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                return method != null && !method.IsAbstract;
            }));

        IEnumerable<Type> TypeHierarchy()
        {
            for (var t = cardType; t != null && typeof(CardModel).IsAssignableFrom(t); t = t.BaseType)
                yield return t;
        }
    }
}