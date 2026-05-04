using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_ShunMod.Core.Registration;
using STS2_ShunMod.Patches;

namespace STS2_ShunMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "STS2_ShunMod";

    private static readonly Harmony Harmony = new(ModId);

    public static void Initialize()
    {
        try
        {
            Harmony.PatchAll();
            ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            Log.Error(ModId + " - 加载失败");
            Log.Error(e.Message);
            return;
        }
        Log.Info(ModId + " - 加载成功!");
    }
}
