using System.Collections.Concurrent;
using System.Reflection;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     第三方模组兼容补丁共享工具 — 类型查找、单例发现。
/// </summary>
internal static class CompatibilityPatchUtil
{
    // ═══════════════════════════════════════════════════════════
    //  Manager 实例缓存
    // ═══════════════════════════════════════════════════════════
    //
    //  缓存设计原因：
    //  ShadowverseEvolutionPointPatch 在 Initialize_Postfix、
    //  ResetAllBoolFlags、FirePointsChanged 中多次调用
    //  FindManagerInstance(_evoMgrType)，每次调用都反射查
    //  Instance 属性 → _instance 字段 → instance 字段。
    //  管理器实例在游戏启动后不会变化，缓存后后续直接返回。
    //
    //  ConcurrentDictionary 保证线程安全，兼容 Harmony 多线程场景。
    //
    // ═══════════════════════════════════════════════════════════
    private static readonly ConcurrentDictionary<Type, object?> ManagerInstanceCache = new();

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

    /// <summary>尝试通过 Instance 属性或 _instance/instance 字段获取单例（结果缓存）。</summary>
    public static object? FindManagerInstance(Type managerType)
    {
        return ManagerInstanceCache.GetOrAdd(managerType, ResolveManagerInstance);
    }

    private static object? ResolveManagerInstance(Type managerType)
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
