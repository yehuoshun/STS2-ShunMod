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
[HarmonyPatch]
public static class InfiniteUpgradeMaxUpgrade
{
    private const int UpgradeCap = 99;

    // ═══════════════════════════════════════════════════════════
    //  目标 Getter 缓存
    // ═══════════════════════════════════════════════════════════
    //
    //  缓存设计原因：
    //  1. 这个补丁需要 patch 基类 + 所有覆写了 MaxUpgradeLevel
    //     的 CardModel 子类。原版游戏有 200+ 卡牌类型，用
    //     TargetMethods() 每次产出都遍历 GetTypes() + 反射查
    //     每个子类的属性 getter，开销巨大。
    //  2. CardModel 子类集在程序集加载后不会变化，只扫一次
    //     就够了。static readonly 由 CLR 类型初始化器保证
    //     只执行一次，隐式线程安全。
    //  3. Harmony 内部对 TargetMethods() 返回的 IEnumerable
    //     只迭代一次，用 List<MethodBase> 替代无影响。
    //
    // ═══════════════════════════════════════════════════════════
    private static readonly List<MethodBase> TargetGetterCache = BuildTargetGetters();

    private static IEnumerable<MethodBase> TargetMethods() => TargetGetterCache;

    private static List<MethodBase> BuildTargetGetters()
    {
        var getters = new List<MethodBase>();

        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null) getters.Add(baseGetter);

        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type)) continue;
            var getter = AccessTools.PropertyGetter(type, nameof(CardModel.MaxUpgradeLevel));
            if (getter != null && getter.DeclaringType == type) getters.Add(getter);
        }

        return getters;
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref int __result)
    {
        if (InfiniteUpgradeSafety.CanUseUnlimitedGrowth(__instance, __result) && __result < UpgradeCap)
            __result = UpgradeCap;

        var serializedLevel = InfiniteUpgradeSerializationContext.Peek();
        if (serializedLevel > __result &&
            InfiniteUpgradeSafety.ShouldAllowSerializedUpgrade(__instance, __result, serializedLevel))
        {
            __result = serializedLevel;
            return;
        }

        var currentLevel = __instance.CurrentUpgradeLevel;
        if (currentLevel > __result &&
            InfiniteUpgradeSafety.ShouldAllowObservedUpgrade(__instance, __result, currentLevel))
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

    public static void Push(int upgradeLevel) => (_stack ??= new Stack<int>()).Push(upgradeLevel);
    public static void Pop() { if (_stack != null && _stack.Count > 0) _stack.Pop(); }
    public static int Peek() => _stack != null && _stack.Count > 0 ? _stack.Peek() : 0;
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

    public static bool ShouldAllowSerializedUpgrade(CardModel card, int originalMax, int savedLevel)
        => savedLevel > originalMax && CanUseUnlimitedGrowth(card, originalMax);

    public static bool ShouldAllowObservedUpgrade(CardModel card, int originalMax, int currentLevel)
        => currentLevel > originalMax && CanUseUnlimitedGrowth(card, originalMax);

    public static int PrepareSerializableUpgradeLevel(SerializableCard save)
    {
        var level = Math.Max(0, save.CurrentUpgradeLevel);
        if (level == 0 || save.Id == null) return level;

        var card = ResolveCanonicalCard(save.Id);
        if (card == null) return level;

        var originalMax = Math.Max(0, card.MaxUpgradeLevel);
        if (level <= originalMax) return level;

        if (CanUseUnlimitedGrowth(card, originalMax)) return level;

        save.CurrentUpgradeLevel = originalMax;
        return originalMax;
    }

    private static CardModel? ResolveCanonicalCard(ModelId id) => ModelDb.GetByIdOrNull<CardModel>(id);

    private static bool IsDrawSensitive(Type cardType)
    {
        return DrawSensitiveCache.GetOrAdd(cardType, ComputeDrawSensitive);
    }

    private static bool ComputeDrawSensitive(Type cardType)
    {
        for (var t = cardType; t != null && typeof(CardModel).IsAssignableFrom(t); t = t.BaseType)
        {
            foreach (var methodName in DrawSensitiveMethods)
            {
                var method = t.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null && !method.IsAbstract) return true;
            }
        }
        return false;
    }
}