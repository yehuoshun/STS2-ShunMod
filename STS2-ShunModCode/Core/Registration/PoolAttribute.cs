namespace STS2_ShunMod.Core.Registration;

/// <summary>
/// 标记类所属的游戏内容池（如无色牌池、角色专属牌池等）。
/// </summary>
/// <remarks>
/// ContentRegistry.RegisterAll 扫描到此属性后，
/// 自动调用 ModHelper.AddModelToPool(poolType, targetType) 完成注册。
///
/// 用法：
/// <code>
/// [Pool(typeof(ColorlessCardPool))]
/// public class MyCard : ShunCard { ... }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PoolAttribute : Attribute
{
    /// <summary>
    /// 目标卡池类型（如 ColorlessCardPool / IroncladCardPool）。
    /// </summary>
    public Type PoolType { get; }

    /// <summary>
    /// 标记类所属的内容池。
    /// </summary>
    /// <param name="poolType">目标卡池类型</param>
    public PoolAttribute(Type poolType)
    {
        PoolType = poolType;
    }
}
