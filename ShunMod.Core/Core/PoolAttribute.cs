namespace ShunMod.Core;

/// <summary>
///     标记卡牌所属的卡池。ContentRegistry 扫描后自动注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class CardPoolAttribute : Attribute
{
    public Type PoolType { get; }

    public CardPoolAttribute(Type poolType)
    {
        PoolType = poolType;
    }
}

/// <summary>
///     标记遗物所属的遗物池。ContentRegistry 扫描后自动注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class RelicPoolAttribute : Attribute
{
    public Type PoolType { get; }

    public RelicPoolAttribute(Type poolType)
    {
        PoolType = poolType;
    }
}

/// <summary>
///     标记自定义事件。ContentRegistry 扫描后收集类型，由 ShunModEventRegistry 注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class EventPoolAttribute : Attribute
{
}