using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace STS2_ShunMod.Core.Registration;

/// <summary>
/// 内容自动注册器 — 扫描程序集中带 [Pool] 属性的类型，自动注册到游戏卡池。
/// </summary>
/// <remarks>
/// 用法：
/// <code>
/// // MainFile.Initialize() 中调用一次：
/// ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());
///
/// // 卡牌类加属性即可，无需手动改 MainFile：
/// [Pool(typeof(ColorlessCardPool))]
/// public class MyCard : ShunCard { ... }
/// </code>
/// </remarks>
public static class ContentRegistry
{
    /// <summary>
    /// 扫描指定程序集中所有非抽象类，将带 [Pool] 属性的类型注册到对应卡池。
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    public static void RegisterAll(Assembly assembly)
    {
        foreach (var type in AssemblyScanner.GetLoadableTypes(assembly))
        {
            // 跳过抽象类（基类、模板等）
            if (type.IsAbstract) continue;

            var poolAttr = type.GetCustomAttribute<PoolAttribute>();
            if (poolAttr == null) continue;

            // 委托给游戏 ModHelper 完成实际注册
            ModHelper.AddModelToPool(poolAttr.PoolType, type);
        }
    }
}
