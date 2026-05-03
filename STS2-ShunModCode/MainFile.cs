using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_ShunMod.Cards;
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
            ModHelper.AddModelToPool(typeof(ColorlessCardPool), typeof(SuperApotheosis));
            Harmony.PatchAll();
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