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
// Patch 2: IsUpgradable → ShunCard 强制可升级（兜底）
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

    public static void Pop()
    {
        if (Stack.Count > 0) Stack.Pop();
    }

    public static int Peek() => Stack.Count > 0 ? Stack.Peek() : 0;
}

/// <summary>
///     Patch 1: 拦截所有 CardModel 子类的 MaxUpgradeLevel getter。
///     参照 STS2Plus UnlimitedGrowthMaxUpgradePatch，用 TargetMethods 动态扫描。
///     优先使用序列化上下文中的存档升级等级（读档时通过 FromSerializable Prefix 推入）。
/// </summary>
[HarmonyPatch]
public static class InfiniteUpgrade_MaxUpgradeLevel
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var count = 0;

        // 1. 基类 virtual getter — 覆盖所有未显式 override 的卡牌
        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null)
        {
            count++;
            yield return baseGetter;
        }

        // 2. 子类 override — 覆盖显式覆写了 getter 的卡牌（如原版降级卡可能有特殊逻辑）
        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type))
                continue;

            var getter = AccessTools.PropertyGetter(type, nameof(CardModel.MaxUpgradeLevel));
            if (getter != null && getter.DeclaringType == type)
            {
                count++;
                yield return getter;
            }
        }

        ShunLogger.Info("无限升级/TargetMethods", $"扫描到 {count} 个 MaxUpgradeLevel getter");
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref int __result)
    {
        try
        {
            // 读档时：FromSerializable 循环调用 UpgradeInternal()，
            // 每次 CurrentUpgradeLevel++ 的 setter 会检查 MaxUpgradeLevel。
            // 如果栈中有存档等级，临时抬高以通过检查。
            var savedLevel = UpgradeSerializationContext.Peek();
            if (savedLevel > __result && savedLevel > __instance.CurrentUpgradeLevel)
            {
                var old = __result;
                __result = savedLevel;
                ShunLogger.Info("无限升级/MaxUpgrade", $"读档模式: {__instance.GetType().Name} {old} → {__result}");
                return;
            }

            // 常规运行时：移除升级上限
            if (__result > 0 && __result < UpgradeConst.MaxUpgradeCap)
            {
                var old = __result;
                __result = UpgradeConst.MaxUpgradeCap;
                ShunLogger.Info("无限升级/MaxUpgrade", $"{__instance.GetType().Name}: {old} → {__result}");
            }
        }
        catch (Exception ex)
        {
            ShunLogger.Error("无限升级/MaxUpgrade", ex);
        }
    }
}

/// <summary>
///     Patch 2（兜底）: 直接拦截 IsUpgradable。
///     优先属性 getter，回退到无参方法。
///     当 Patch 1 因版本 API 变化未命中时，确保 ShunCard 始终可升级。
/// </summary>
[HarmonyPatch]
public static class InfiniteUpgrade_IsUpgradable
{
    private static MethodBase? TargetMethod()
    {
        // 优先属性 getter
        var getter = AccessTools.PropertyGetter(typeof(CardModel), "IsUpgradable");
        if (getter != null)
        {
            ShunLogger.Info("无限升级/IsUpgradable", "匹配 IsUpgradable getter");
            return getter;
        }

        // 回退到无参方法
        var method = AccessTools.Method(typeof(CardModel), "IsUpgradable", Type.EmptyTypes);
        if (method != null)
        {
            ShunLogger.Info("无限升级/IsUpgradable", "匹配 IsUpgradable() 方法");
            return method;
        }

        ShunLogger.Warn("无限升级/IsUpgradable", "未找到目标，补丁跳过");
        return null;
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (!__result && __instance is ShunCard)
        {
            __result = true;
            ShunLogger.Info("无限升级/IsUpgradable", $"{__instance.GetType().Name} 兜底强制可升级");
        }
    }
}

/// <summary>
///     Patch 3: 拦截 CardModel.FromSerializable，保留存档中的升级等级。
///     参考 STS2Plus UnlimitedGrowthDeserializePatch。
///     读档流程：
///     <code>
///     for (int i = 0; i &lt; save.CurrentUpgradeLevel; i++)
///         card.UpgradeInternal();  // CurrentUpgradeLevel++
///     </code>
///     每次 CurrentUpgradeLevel++ 都会走 setter：
///     <code>
///     if (value &gt; MaxUpgradeLevel) throw;
///     </code>
///     所以 Prefix 把存档等级 Push 到 Context，
///     Postfix（Patch 1）读到后临时抬高 MaxUpgradeLevel 以通过校验。
/// </summary>
[HarmonyPatch]
public static class InfiniteUpgrade_Deserialize
{
    private static MethodBase? TargetMethod()
    {
        var types = new[] { typeof(SerializableCard) };
        var method = AccessTools.Method(typeof(CardModel), "FromSerializable", types);

        if (method != null)
        {
            ShunLogger.Info("无限升级/反序列化", $"目标: CardModel.FromSerializable(SerializableCard)");
            return method;
        }

        // 回退：可能的其他签名
        method = AccessTools.Method(typeof(CardModel), "FromSerializable");
        if (method != null)
        {
            ShunLogger.Info("无限升级/反序列化", $"模糊匹配: {method}");
            return method;
        }

        ShunLogger.Warn("无限升级/反序列化", "未找到目标，补丁跳过");
        return null;
    }

    [HarmonyPrefix]
    private static void Prefix(SerializableCard save)
    {
        if (save.CurrentUpgradeLevel > 1)
        {
            UpgradeSerializationContext.Push(save.CurrentUpgradeLevel);
            ShunLogger.Info("无限升级/反序列化", $"Push 存档等级={save.CurrentUpgradeLevel}");
        }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        UpgradeSerializationContext.Pop();
        if (__exception != null)
            ShunLogger.Error("无限升级/反序列化", __exception);
        return __exception;
    }
}