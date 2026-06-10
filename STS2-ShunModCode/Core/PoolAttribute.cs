namespace STS2ShunMod.Core;

/// <summary>
///     标记类所属的游戏内容池（卡池/遗物池）。
///     ContentRegistry.RegisterAll 扫描到此属性后自动注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PoolAttribute : Attribute
{
    public Type PoolType { get; }

    public PoolAttribute(Type poolType)
    {
        PoolType = poolType;
    }
}