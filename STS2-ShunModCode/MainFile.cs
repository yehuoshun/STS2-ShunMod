using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "STS2-ShunMod";

    private static readonly Harmony _harmony = new(ModId);

    public static void Initialize()
    {
        ShunLogger.Summary(ModId);

        try
        {
            _harmony.PatchAll();
            ShunLogger.Info(ModId, $"Harmony PatchAll 完成，共 {_harmony.GetPatchedMethods().Count()} 个方法已注入");
        }
        catch (Exception e)
        {
            ShunLogger.Error(ModId, $"{e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                ShunLogger.Error(ModId, $"  → 内层: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
            ShunLogger.Error(ModId, "Harmony 补丁加载失败，跳过 → 继续内容注册");
        }

        try
        {
            ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());
            ShunLogger.Info(ModId, "内容注册完成");
        }
        catch (Exception e)
        {
            ShunLogger.Error(ModId, $"内容注册失败: {e.Message}");
        }

        Log.Info($"{ModId} - 加载完成! ({_harmony.GetPatchedMethods().Count()} patches)");
    }
}