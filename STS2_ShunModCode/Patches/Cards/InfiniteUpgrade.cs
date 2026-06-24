using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
namespace STS2ShunMod.STS2_ShunModCode.Patches.Cards;

// ════════════════════════════════ 主 Patch：MaxUpgradeLevel getter ════════════════════════════════

[HarmonyPatch]
public static class InfiniteUpgrade_MaxUpgrade
{
    private const int UpgradeCap = 99;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null) yield return baseGetter;

        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type)) continue;
            var getter = AccessTools.PropertyGetter(type, nameof(CardModel.MaxUpgradeLevel));
            if (getter != null && getter.DeclaringType == type) yield return getter;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref int __result)
    {
        if (InfiniteUpgrade_Safety.CanUseUnlimitedGrowth(__instance, __result) && __result < UpgradeCap)
            __result = UpgradeCap;

        var serializedLevel = InfiniteUpgrade_SerializationContext.Peek();
        if (serializedLevel > __result &&
            InfiniteUpgrade_Safety.ShouldAllowSerializedUpgrade(__instance, __result, serializedLevel))
        {
            __result = serializedLevel;
            return;
        }

        var currentLevel = __instance.CurrentUpgradeLevel;
        if (currentLevel > __result &&
            InfiniteUpgrade_Safety.ShouldAllowObservedUpgrade(__instance, __result, currentLevel))
            __result = currentLevel;
    }
}

// ════════════════════════════════ 反序列化 Patch ════════════════════════════════

[HarmonyPatch(typeof(CardModel), "FromSerializable")]
public static class InfiniteUpgrade_Deserialize
{
    private static void Prefix(SerializableCard save)
    {
        var level = InfiniteUpgrade_Safety.PrepareSerializableUpgradeLevel(save);
        InfiniteUpgrade_SerializationContext.Push(level);
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        InfiniteUpgrade_SerializationContext.Pop();
        return __exception;
    }
}

// ════════════════════════════════ 线程安全序列化上下文 ════════════════════════════════

internal static class InfiniteUpgrade_SerializationContext
{
    [ThreadStatic] private static Stack<int>? _stack;

    public static void Push(int upgradeLevel) => (_stack ??= new Stack<int>()).Push(upgradeLevel);
    public static void Pop() { if (_stack != null && _stack.Count > 0) _stack.Pop(); }
    public static int Peek() => _stack != null && _stack.Count > 0 ? _stack.Peek() : 0;
}

// ════════════════════════════════ 安全检测 ════════════════════════════════════

internal static class InfiniteUpgrade_Safety
{
    private static readonly string[] DrawSensitiveMethods =
        ["BeforeHandDraw", "AfterCardDrawn", "ModifyHandDraw"];

    private static readonly object SyncRoot = new();
    private static readonly Dictionary<Type, bool> DrawSensitiveCache = new();

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
        lock (SyncRoot)
        {
            if (DrawSensitiveCache.TryGetValue(cardType, out var cached)) return cached;
        }

        var sensitive = false;
        for (var t = cardType; t != null && typeof(CardModel).IsAssignableFrom(t); t = t.BaseType)
        {
            foreach (var methodName in DrawSensitiveMethods)
            {
                var method = t.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null && !method.IsAbstract) { sensitive = true; break; }
            }
            if (sensitive) break;
        }

        lock (SyncRoot) { DrawSensitiveCache[cardType] = sensitive; }
        return sensitive;
    }
}