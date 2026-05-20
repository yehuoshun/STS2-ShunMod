using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限升级系统 — 参考 STS2Plus UnlimitedGrowth 实现
//
// Patch 1: MaxUpgradeLevel getter → TargetMethods 拉到 99
// Patch 2: IsUpgradable getter → ShunCard 强制可升级（兜底）
// Patch 3: FromSerializable → 保留存档升级等级
// ════════════════════════════════════════════════════════

internal static class UpgradeConst
{
    public const int MaxUpgradeCap = 99;
}

internal static class UpgradeSerializationContext
{
    [ThreadStatic]
    private static Stack<int>? _stack;
    private static Stack<int> Stack => _stack ??= new Stack<int>();
    public static void Push(int v) => Stack.Push(v);
    public static void Pop() { if (Stack.Count > 0) Stack.Pop(); }
    public static int Peek() => Stack.Count > 0 ? Stack.Peek() : 0;
}

/// <summary>
///     Patch 1: 拦截所有 CardModel 子类的 MaxUpgradeLevel getter。
///     参照 STS2Plus UnlimitedGrowthMaxUpgradePatch，用 TargetMethods 动态扫描。
///     优先使用序列化上下文中的存档升级等级。
/// </summary>
[HarmonyPatch]
public static class InfiniteUpgrade_MaxUpgradeLevel
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null)
            yield return baseGetter;

        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type))
                continue;

            var getter = AccessTools.PropertyGetter(type, nameof(CardModel.MaxUpgradeLevel));
            if (getter != null && getter.DeclaringType == type)
                yield return getter;
        }
    }

    private static void Postfix(CardModel __instance, ref int __result)
    {
        if (__result > 0 && __result < UpgradeConst.MaxUpgradeCap) __result = UpgradeConst.MaxUpgradeCap;
    }
}

/// <summary>
///     Patch 2（兜底）: 直接拦截 IsUpgradable getter。
///     当 Patch 1 因版本 API 变化未命中时，确保 ShunCard 始终可升级。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
public static class InfiniteUpgrade_IsUpgradable
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (!__result && __instance is ShunCard) __result = true;
    }
}

/// <summary>
///     Patch 3: 拦截 CardModel.FromSerializable，保留存档中的升级等级。
///     参考 STS2Plus UnlimitedGrowthDeserializePatch。
/// </summary>
[HarmonyPatch(typeof(CardModel), "FromSerializable")]
public static class InfiniteUpgrade_Deserialize
{
    private static void Prefix(SerializableCard save)
    {
        if (save.CurrentUpgradeLevel > 0)
            UpgradeSerializationContext.Push(save.CurrentUpgradeLevel);
    }

    private static void Finalizer(Exception? __exception)
    {
        UpgradeSerializationContext.Pop();
    }
}