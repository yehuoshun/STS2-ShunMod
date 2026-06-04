using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "STS2-ShunMod";

    private static readonly Harmony _harmony = new(ModId);

    public static void Initialize()
    {
        try
        {
            _harmony.PatchAll();
        }
        catch (Exception e)
        {
            Log.Error($"[{ModId}] {e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                Log.Error($"[{ModId}]   → 内层: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
            Log.Error($"[{ModId}] Harmony 补丁加载失败，跳过 → 继续内容注册");
        }

        try
        {
            ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            Log.Error($"[{ModId}] 内容注册失败: {e.Message}");
        }
    }
}