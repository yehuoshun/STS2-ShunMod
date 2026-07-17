using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;

namespace ShunMod.Core.Core;

/// <summary>
///     第三方模组兼容补丁共享工具 — 类型查找。
/// </summary>
public static class CompatibilityPatchUtil
{
    /// <summary>查找兼容模组类型，未找到时自动日志跳过。</summary>
    public static Type? FindPatchType(string modId, string ns, string typeName)
    {
        var type = FindType(ns, typeName);
        if (type == null)
            Log.Info($"[{modId}] {typeName} not detected, skipping patch");
        return type;
    }

    /// <summary>跨程序集查找类型</summary>
    public static Type? FindType(string ns, string typeName)
    {
        var fullName = $"{ns}.{typeName}";
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(asm => asm.GetType(fullName))
            .FirstOrDefault(t => t != null);
    }
}