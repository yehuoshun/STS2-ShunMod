using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace STS2_ShunMod.Core.Registration;

/// <summary>
/// 扫描程序集中带 [Pool] 属性的类型，自动注册到游戏卡池。
///
/// 用法：
///   MainFile.Initialize() 中调 ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());
///   卡牌类加 [Pool(typeof(ColorlessCardPool))]
/// </summary>
public static class ContentRegistry
{
    public static void RegisterAll(Assembly assembly)
    {
        foreach (var type in AssemblyScanner.GetLoadableTypes(assembly))
        {
            if (type.IsAbstract) continue;

            var poolAttr = type.GetCustomAttribute<PoolAttribute>();
            if (poolAttr == null) continue;

            ModHelper.AddModelToPool(poolAttr.PoolType, type);
        }
    }
}
