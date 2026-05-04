using System.Reflection;

namespace STS2_ShunMod.Core.Registration;

/// <summary>
/// 安全程序集类型加载器 — 兼容 Mono / IL2CPP 平台。
/// </summary>
/// <remarks>
/// 在 Android（Mono）、iOS（IL2CPP）等非标准 .NET 运行时上，
/// Assembly.GetTypes() 可能抛出 ReflectionTypeLoadException。
/// 本类捕获该异常并返回成功加载的类型子集，避免整个扫描流程崩溃。
/// </remarks>
internal static class AssemblyScanner
{
    /// <summary>
    /// 安全获取程序集中所有可加载的类型。
    /// </summary>
    /// <param name="assembly">目标程序集</param>
    /// <returns>成功加载的类型列表</returns>
    public static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // 返回成功加载的类型，跳过无法加载的
            return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }
    }
}
