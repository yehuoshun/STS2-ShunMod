using System.Reflection;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShunMod.Core.Core.Helpers;

/// <summary>
///     DynamicVar 反射赋值工具 — 给 StringVar 的字符串属性赋值。
///     反射查出 StringVar 上唯一可写的 string 属性，避免猜属性名。
///     用 dynamic 接收 DynamicVars（实际类型 DynamicVarSet，非公开 API），
///     避免依赖不可见类型的编译时引用。
/// </summary>
public static class DynamicVarHelper
{
    /// <summary>StringVar.Value 属性信息缓存（类初始化时解析一次，后续直接 SetValue）。</summary>
    private static readonly PropertyInfo? StringValueProp = typeof(StringVar)
        .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .FirstOrDefault(p => p.CanWrite && p.PropertyType == typeof(string));

    /// <summary>给 DynamicVars 中指定 key 的 StringVar 设置字符串值。</summary>
    public static void SetStrValue(dynamic dynamicVars, string key, string val)
    {
        if (!dynamicVars.TryGetValue(key, out object? dv)) return;
        if (dv is not StringVar sv) return;
        StringValueProp?.SetValue(sv, val);
    }
}