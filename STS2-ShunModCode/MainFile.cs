using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod;

/// <summary>
///     Mod 入口 — 逐个应用 Harmony 补丁，单个炸不影响其他。
///     参照 STS2Plus 的 PatchAllSafe 模式：每类独立 try-catch。
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "STS2-ShunMod";

    private static readonly Harmony _harmony = new(ModId);

    public static void Initialize()
    {
        ShunLogger.Summary(ModId);

        var assembly = Assembly.GetExecutingAssembly();

        // Phase 1: 逐个应用 Harmony 补丁（每类独立 try-catch，一个炸不拖全 mod）
        int patched = 0, failed = 0;
        foreach (var type in GetPatchTypes(assembly))
        {
            try
            {
                var processor = _harmony.CreateClassProcessor(type);
                var methods = processor.Patch();
                patched += methods.Count;
                ShunLogger.Info(ModId, $"✓ {type.Name} ({methods.Count} 方法)");
            }
            catch (Exception ex)
            {
                failed++;
                ShunLogger.Error(ModId, $"{type.Name} 失败: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                    ShunLogger.Error(ModId, $"  → 内层: {ex.InnerException.Message}");
            }
        }

        ShunLogger.Info(ModId, $"Harmony: {patched} 成功 / {failed} 失败");

        // Phase 2: 内容注册（不受补丁成败影响）
        try
        {
            ContentRegistry.RegisterAll(assembly);
            ShunLogger.Info(ModId, "内容注册完成");
        }
        catch (Exception e)
        {
            ShunLogger.Error(ModId, $"内容注册失败: {e.Message}");
        }

        Log.Info($"{ModId} - 加载完成! ({patched} patches / {failed} errors)");
    }

    /// <summary>
    ///     扫描 assembly 中所有带 [HarmonyPatch] 的类，
    ///     按声明顺序返回。忽略不含补丁方法的抽象/静态辅助类。
    /// </summary>
    private static IEnumerable<Type> GetPatchTypes(Assembly assembly)
    {
        foreach (var type in GetSafeTypes(assembly))
        {
            if (!type.IsClass || type.IsAbstract)
                continue;

            // 类本身有 [HarmonyPatch]（标准模式）→ 纳入
            if (type.GetCustomAttribute<HarmonyPatch>() != null)
            {
                yield return type;
                continue;
            }

            // 类的方法上有 [HarmonyPatch]（方法级注解模式）→ 也纳入
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (method.GetCustomAttribute<HarmonyPatch>() != null)
                {
                    yield return type;
                    break;
                }
            }
        }
    }

    /// <summary>
    ///     安全获取类型，处理 ReflectionTypeLoadException。
    /// </summary>
    private static Type[] GetSafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e)
        { return e.Types.Where(t => t != null).Cast<Type>().ToArray(); }
    }
}