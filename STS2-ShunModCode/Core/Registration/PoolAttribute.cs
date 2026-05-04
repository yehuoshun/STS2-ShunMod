namespace STS2_ShunMod.Core.Registration;

/// <summary>
/// 标记卡牌/遗物/能力类所属的内容池。
/// ContentRegistry.RegisterAll 扫描到此属性后自动调用 ModHelper.AddModelToPool。
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
