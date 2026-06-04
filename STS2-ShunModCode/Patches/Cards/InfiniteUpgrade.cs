using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Logging;

namespace STS2_ShunMod.Patches;

/// <summary>
///     无限升级系统 — 参照 STS2Plus UnlimitedGrowth 重写。
///
///     核心机制：
///     1. MaxUpgradeLevel getter → 对所有可升级卡牌上限改为 99
///     2. 安全检测 → 排除抽牌敏感卡牌，防止无限循环
///     3. 反序列化支持 → 读档时正确恢复超出原始上限的升级等级
/// </summary>

// ════════════════════════════════ 主 Patch：MaxUpgradeLevel getter ════════════════════════════════

/// <summary>
///     Patch 所有 CardModel 及其子类的 MaxUpgradeLevel getter。
///     使用 TargetMethods 代替 [HarmonyPatch] 注解，因为后者只 Patch 声明类。
/// </summary>
[HarmonyPatch]
public static class InfiniteUpgrade_MaxUpgrade
{
    private const int UpgradeCap = 99;

    /// <summary>
    ///     产出 CardModel.MaxUpgradeLevel getter（基类）+ 所有子类 override getter。
    /// </summary>
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // 基类 getter
        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null)
            yield return baseGetter;

        // 所有子类 override getter
        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type))
                continue;

            var getter = AccessTools.PropertyGetter(type, nameof(CardModel.MaxUpgradeLevel));
            if (getter != null && getter.DeclaringType == type)
                yield return getter;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref int __result)
    {
        // ═══ 无限升级激活（ShunMod 始终启用）═══
        if (InfiniteUpgrade_Safety.CanUseUnlimitedGrowth(__instance, __result) && __result < UpgradeCap)
            __result = UpgradeCap;

        // ═══ 反序列化支持：读档时恢复超出原始上限的等级 ═══
        var serializedLevel = InfiniteUpgrade_SerializationContext.Peek();
        if (serializedLevel > __result &&
            InfiniteUpgrade_Safety.ShouldAllowSerializedUpgrade(__instance, __result, serializedLevel))
        {
            __result = serializedLevel;
            return;
        }

        // ═══ 运行时升级支持：当前等级可能因其他 mod/机制超出原始上限 ═══
        var currentLevel = __instance.CurrentUpgradeLevel;
        if (currentLevel > __result &&
            InfiniteUpgrade_Safety.ShouldAllowObservedUpgrade(__instance, __result, currentLevel))
            __result = currentLevel;
    }
}

// ════════════════════════════════ 反序列化 Patch ════════════════════════════════

/// <summary>
///     拦截 CardModel.FromSerializable，在反序列化前存储原始升级等级到上下文。
///     这样 MaxUpgradeLevel getter 被调用时能知道读档的原始值。
/// </summary>
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

/// <summary>
///     线程静态栈，在反序列化期间存储升级等级。
///     MaxUpgradeLevel getter 的 Postfix 通过 Peek 读取。
/// </summary>
internal static class InfiniteUpgrade_SerializationContext
{
    [ThreadStatic] private static Stack<int>? _stack;

    public static void Push(int upgradeLevel)
    {
        (_stack ??= new Stack<int>()).Push(upgradeLevel);
    }

    public static void Pop()
    {
        if (_stack != null && _stack.Count > 0)
            _stack.Pop();
    }

    public static int Peek()
    {
        return _stack != null && _stack.Count > 0 ? _stack.Peek() : 0;
    }
}

// ════════════════════════════════ 安全检测 ════════════════════════════════════

/// <summary>
///     安全检测：防止抽牌敏感卡牌无限升级导致无限循环。
///     参照 STS2Plus UnlimitedGrowthSafety，简化为反射检查。
/// </summary>
internal static class InfiniteUpgrade_Safety
{
    // 抽牌敏感方法名：只检查与抽牌直接相关的方法。
    // OnPlay/OnTurnEndInHand 不在此列——它们需要 IL 字节码分析
    // 才能判断是否真正调用了抽牌命令（参照 STS2Plus MethodReferencesDrawFlow），
    // 简化实现中不做分析，让这些卡牌通过。
    private static readonly string[] DrawSensitiveMethods =
        ["BeforeHandDraw", "AfterCardDrawn", "ModifyHandDraw"];

    private static readonly object SyncRoot = new();
    private static readonly Dictionary<Type, bool> DrawSensitiveCache = new();
    private static readonly HashSet<string> ClampedWarnings = new(StringComparer.Ordinal);

    // ═══ 公开 API ═══

    /// <summary>卡牌是否可以使用无限升级</summary>
    public static bool CanUseUnlimitedGrowth(CardModel card, int originalMaxUpgradeLevel)
    {
        if (originalMaxUpgradeLevel <= 0) return false;
        return !IsDrawSensitive(card.GetType());
    }

    /// <summary>反序列化：允许超出原始上限的升级等级</summary>
    public static bool ShouldAllowSerializedUpgrade(CardModel card, int originalMax, int savedLevel)
    {
        return savedLevel > originalMax && CanUseUnlimitedGrowth(card, originalMax);
    }

    /// <summary>运行时：允许超出原始上限的升级等级</summary>
    public static bool ShouldAllowObservedUpgrade(CardModel card, int originalMax, int currentLevel)
    {
        return currentLevel > originalMax && CanUseUnlimitedGrowth(card, originalMax);
    }

    /// <summary>
    ///     反序列化准备：如果存档等级超出原始上限但卡牌不安全，则钳制到原始上限。
    ///     返回调整后的升级等级。
    /// </summary>
    public static int PrepareSerializableUpgradeLevel(SerializableCard save)
    {
        var level = Math.Max(0, save.CurrentUpgradeLevel);
        if (level == 0 || save.Id == null) return level;

        var card = ResolveCanonicalCard(save.Id);
        if (card == null) return level;

        var originalMax = Math.Max(0, card.MaxUpgradeLevel);
        if (level <= originalMax) return level;

        // 安全检查：不安全的卡牌钳制回原始上限
        if (CanUseUnlimitedGrowth(card, originalMax)) return level;

        save.CurrentUpgradeLevel = originalMax;
        LogClamped(card, level, originalMax);
        return originalMax;
    }

    // ═══ 内部实现 ═══

    private static CardModel? ResolveCanonicalCard(ModelId id)
    {
        return ModelDb.GetByIdOrNull<CardModel>(id);
    }

    private static bool IsDrawSensitive(Type cardType)
    {
        lock (SyncRoot)
        {
            if (DrawSensitiveCache.TryGetValue(cardType, out var cached))
                return cached;
        }

        var sensitive = false;
        for (var t = cardType; t != null && typeof(CardModel).IsAssignableFrom(t); t = t.BaseType)
        {
            foreach (var methodName in DrawSensitiveMethods)
            {
                var method = t.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null && !method.IsAbstract)
                {
                    sensitive = true;
                    break;
                }
            }

            if (sensitive) break;
        }

        lock (SyncRoot)
        {
            DrawSensitiveCache[cardType] = sensitive;
        }

        return sensitive;
    }

    private static void LogClamped(CardModel card, int saved, int clamped)
    {
        var key = $"{card.Id}:{saved}→{clamped}";
        lock (SyncRoot)
        {
            if (!ClampedWarnings.Add(key)) return;
        }

        Log.Info($"[无限升级] [WARN] 读档钳制 {card.Id} 升级等级 {saved}→{clamped}（卡牌含抽牌行为，防止死循环）");
    }
}