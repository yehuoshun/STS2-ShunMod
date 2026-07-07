using System.Reflection;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     DynamicVar 反射赋值工具 — 给 StringVar 的字符串属性赋值。
///
///     不猜属性名：直接反射查出 StringVar 上唯一可写的 string 属性，
///     避免游戏 API 改名后静默失败。
///
///     设计中需要注意的事：
///     StringVar 的字符串属性在不同游戏版本中可能有不同名称，
///     用反射找可写 string 属性比硬编码属性名更健壮。
///     代价是每次调用有微量反射开销，但事件选项刷新频率极低，可以忽略。
/// </summary>
public static class DynamicVarHelper
{
    /// <summary>给 DynamicVars 字典中指定 key 的 StringVar 设置字符串值。</summary>
    public static void SetStrValue(IDictionary<string, DynamicVar> dynamicVars, string key, string val)
    {
        if (!dynamicVars.TryGetValue(key, out var dv) || dv is not StringVar sv) return;
        var prop = sv.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(p => p.CanWrite && p.PropertyType == typeof(string));
        prop?.SetValue(sv, val);
    }
}