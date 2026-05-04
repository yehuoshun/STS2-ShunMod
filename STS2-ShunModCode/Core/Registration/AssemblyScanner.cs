using System.Reflection;

namespace STS2_ShunMod.Core.Registration;

/// <summary>
/// 安全加载程序集中的类型，捕获 ReflectionTypeLoadException
/// （Mono/IL2CPP 平台如 Android/iOS 可能出现）。
/// </summary>
internal static class AssemblyScanner
{
    public static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }
    }
}
