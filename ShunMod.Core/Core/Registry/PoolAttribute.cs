namespace ShunMod.Core.Core.Registry;

/// <summary>
///     标记卡牌所属的卡池。ContentRegistry 扫描后自动注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class CardPoolAttribute(Type poolType) : Attribute
{
    public Type PoolType { get; } = poolType;
}

/// <summary>
///     标记遗物所属的遗物池。ContentRegistry 扫描后自动注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class RelicPoolAttribute(Type poolType) : Attribute
{
    public Type PoolType { get; } = poolType;
}


