using System.Reflection;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     第三方模组兼容补丁共享工具 — 类型查找、单例发现。
/// </summary>
internal static class CompatibilityPatchUtil
{
    /// <summary>跨程序集查找类型</summary>
    public static Type? FindType(string ns, string typeName)
    {
        var fullName = $"{ns}.{typeName}";
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>尝试通过 Instance 属性或 _instance/instance 字段获取单例</summary>
    public static object? FindManagerInstance(Type managerType)
    {
        var prop = managerType.GetProperty("Instance",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (prop != null) return prop.GetValue(null);

        return managerType.GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? managerType.GetField("instance",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null);
    }
}
