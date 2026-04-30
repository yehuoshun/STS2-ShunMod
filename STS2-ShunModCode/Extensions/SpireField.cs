using System.Runtime.CompilerServices;

namespace STS2_ShunMod.Extensions;

/// <summary>
/// 轻量级 ConditionalWeakTable 封装，用于给现有类型附加额外数据。
/// 来源：YuWanCard / BaseLib 模式。
/// </summary>
public class SpireField<TKey, TVal> where TKey : class
{
    private readonly ConditionalWeakTable<TKey, object?> _table = new();
    private readonly Func<TKey, TVal?> _defaultVal;

    public SpireField(Func<TVal?> defaultVal) : this(_ => defaultVal()) { }

    public SpireField(Func<TKey, TVal?> defaultVal)
    {
        _defaultVal = defaultVal;
    }

    public TVal? Get(TKey obj)
    {
        if (_table.TryGetValue(obj, out var result)) return (TVal?)result;
        _table.Add(obj, result = _defaultVal(obj));
        return (TVal?)result;
    }

    public void Set(TKey obj, TVal? val) => _table.AddOrUpdate(obj, val);

    public TVal? this[TKey obj]
    {
        get => Get(obj);
        set => Set(obj, value);
    }
}
