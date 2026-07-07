using System.Reflection;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     DynamicVar 反射赋值工具 — 给 StringVar 的字符串属性赋值。
///
///     不猜属性名：直接反射查出 StringVar 上唯一可写的 string 属性，
///     避免游戏 API 改名后静默失败。
///
///     用 dynamic 接收 dynamicVars 参数的原因：
///     EventModel.DynamicVars 属性的实际类型是 DynamicVarSet（非公开 API），
///     不是 IDictionary&lt;string, DynamicVar&gt;，不能直接强转。
///     用 dynamic 让 DLR 在运行时解析 TryGetValue 调用，
///     避免依赖非公开类型的编译时引用。
///     该方法在事件选项刷新时调用（频率极低），dynamic 开销可忽略。
/// </summary>
public static class DynamicVarHelper
{
    /// <summary>给 DynamicVars 中指定 key 的 StringVar 设置字符串值。</summary>
    public static void SetStrValue(dynamic dynamicVars, string key, string val)
    {
        if (!dynamicVars.TryGetValue(key, out object? dv) || dv is not StringVar sv) return;
        var prop = sv.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(p => p.CanWrite && p.PropertyType == typeof(string));
        prop?.SetValue(sv, val);
    }
}