using System.Diagnostics.CodeAnalysis;

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

/// <summary>
///     标记自定义事件。ContentRegistry 扫描后收集类型，由 ShunModEventRegistry 注册。
///     当前无事件使用，作为框架预留保留。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "框架预留: 未来事件类标记[EventPool]时使用, ContentRegistry反射扫描")]
public class EventPoolAttribute : Attribute
{
}