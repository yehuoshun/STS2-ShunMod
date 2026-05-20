using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限升级 — 完全复制 STS2Plus UnlimitedGrowth 实现
//
// UnlimitedGrowthMaxUpgradePatch  → TargetMethods + Postfix
// UnlimitedGrowthDeserializePatch  → FromSerializable Prefix + Finalizer
// UnlimitedGrowthSerializationContext → ThreadStatic Stack
// ════════════════════════════════════════════════════════

/// <summary>
///     序列化上下文 — 完全复制 STS2Plus UnlimitedGrowthSerializationContext。
///     ThreadStatic Stack，在 FromSerializable → MaxUpgradeLevel 之间传递升级等级。
/// </summary>
internal static class UpgradeSerializationContext
{
    [ThreadStatic]
    private static Stack<int>? _stack;

    private static Stack<int> Stack => _stack ??= new Stack<int>();

    public static void Push(int upgradeLevel) => Stack.Push(upgradeLevel);

    public static void Pop()
    {
        if (Stack.Count > 0)
            Stack.Pop();
    }

    public static int Peek() => Stack.Count > 0 ? Stack.Peek() : 0;
}

/// <summary>
///     完全复制 STS2Plus UnlimitedGrowthDeserializePatch。
/// </summary>
[HarmonyPatch(typeof(CardModel), "FromSerializable")]
public static class UnlimitedGrowthDeserializePatch
{
    private static void Prefix(SerializableCard save)
    {
        int upgradeLevel = save.CurrentUpgradeLevel;
        UpgradeSerializationContext.Push(upgradeLevel);
    }

    private static void Finalizer(Exception? __exception)
    {
        UpgradeSerializationContext.Pop();
    }
}

/// <summary>
///     完全复制 STS2Plus UnlimitedGrowthMaxUpgradePatch。
///     TargetMethods 动态扫描所有 CardModel 子类的 MaxUpgradeLevel getter override。
/// </summary>
[HarmonyPatch]
public static class UnlimitedGrowthMaxUpgradePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), "MaxUpgradeLevel");
        if (baseGetter != null)
            yield return baseGetter;

        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type))
                continue;

            var getter = AccessTools.PropertyGetter(type, "MaxUpgradeLevel");
            if (getter != null && getter.DeclaringType == type)
                yield return getter;
        }
    }

    private static void Postfix(CardModel __instance, ref int __result)
    {
        // 原始 MaxUpgradeLevel <= 0 的卡不干预（不可升级的卡）
        if (__result <= 0)
            return;

        // 1. 优先处理序列化上下文：存档中有高于原始上限的升级等级
        int savedLevel = UpgradeSerializationContext.Peek();
        if (savedLevel > __result)
        {
            __result = savedLevel;
            return;
        }

        // 2. 当前升级等级已超过原始上限 → 保持不变
        int currentLevel = __instance.CurrentUpgradeLevel;
        if (currentLevel > __result)
        {
            __result = currentLevel;
            return;
        }

        // 3. 拉升上限到 99
        if (__result < 99)
            __result = 99;
    }
}