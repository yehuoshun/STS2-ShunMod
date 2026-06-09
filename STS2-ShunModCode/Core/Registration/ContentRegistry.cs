using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Core.Registration;

/// <summary>
///     内容自动注册器 — 扫描 [Pool] 类型注册到卡池，
///     自动检测 EventModel 子类收集类型（实例在 ModelDb.Init 时创建）。
///     参照 YuWanCard ContentRegistry。
/// </summary>
public static class ContentRegistry
{
    /// <summary>
    ///     EventModel 子类类型集合，由 ShunModEventRegistry 在 ModelDb.Init 时消费。
    /// </summary>
    public static readonly HashSet<Type> EventTypes = [];

    /// <summary>
    ///     扫描程序集中所有非抽象类，注册 [Pool] 类型，收集 EventModel 子类。
    /// </summary>
    public static void RegisterAll(Assembly assembly)
    {
        int poolCount = 0, eventCount = 0;

        foreach (var type in AssemblyScanner.GetLoadableTypes(assembly))
        {
            if (type.IsAbstract) continue;

            // [Pool] 属性 → 注册到对应卡池
            var poolAttr = type.GetCustomAttribute<PoolAttribute>();
            if (poolAttr != null)
            {
                ModHelper.AddModelToPool(poolAttr.PoolType, type);
                poolCount++;
            }

            // 自动检测 EventModel 子类 → 收集类型
            // 实例创建交给 ModelDbInit_SafePatch 处理，避免与 ModelDb.Init 冲突导致 DuplicateModelException
            if (typeof(EventModel).IsAssignableFrom(type))
            {
                EventTypes.Add(type);
                eventCount++;
            }
        }
    }
}