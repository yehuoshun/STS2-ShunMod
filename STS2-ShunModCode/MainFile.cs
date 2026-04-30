using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_ShunMod.Cards;
using STS2_ShunMod.Events;
using STS2_ShunMod.Patches;

namespace STS2_ShunMod;

public static class MainFile
{
    public const string ModId = "STS2_ShunMod";

    private static readonly Harmony Harmony = new(ModId);

    static MainFile()
    {
        Harmony.PatchAll();
    }
}

/// <summary>
/// 在主菜单加载时注册自定义内容（卡牌 + 事件）。
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
public static class ContentRegistrationPatch
{
    private static bool _registered;

    static void Postfix()
    {
        if (_registered) return;
        _registered = true;

        // 注册卡牌
        ModHelper.AddModelToPool(typeof(ColorlessCardPool), typeof(SuperApotheosis));

        // 注册事件
        EventRegistry.Register(new RelicExchangeEvent());
    }
}
