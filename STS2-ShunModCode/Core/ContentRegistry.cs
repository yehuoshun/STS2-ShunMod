using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace STS2ShunMod.Core;

/// <summary>
///     内容自动注册器 — 扫描 [Pool] 类型并注册到对应池。
/// </summary>
public static class ContentRegistry
{
    /// <summary>
    ///     扫描程序集中所有非抽象类，注册 [Pool] 类型。
    /// </summary>
    public static int RegisterAll(Assembly assembly)
    {
        var count = 0;
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract) continue;

            var poolAttr = type.GetCustomAttribute<PoolAttribute>();
            if (poolAttr == null) continue;

            ModHelper.AddModelToPool(poolAttr.PoolType, type);
            count++;
        }
        return count;
    }
}