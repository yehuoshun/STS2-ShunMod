using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_ShunMod.Cards;
using STS2_ShunMod.Events;
using STS2_ShunMod.Patches;

namespace STS2_ShunMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "STS2_ShunMod";

    private static readonly Harmony Harmony = new(ModId);

    public static void Initialize()
    {
        Harmony.PatchAll();
        ModHelper.AddModelToPool(typeof(ColorlessCardPool), typeof(SuperApotheosis));
        EventRegistry.Register(new RelicExchangeEvent());
    }
}