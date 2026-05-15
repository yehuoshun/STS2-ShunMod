using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 药水填充前移 + 混沌药水保底。
/// 使用/丢弃后后方药水向前填充，若无混沌药水则自动补充。
/// </summary>
[HarmonyPatchCategory("Gameplay")]
internal static class PotionFillForwardPatch
{
    private static readonly System.Reflection.FieldInfo HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");
    private static readonly System.Reflection.FieldInfo PlayerField =
        AccessTools.Field(typeof(NPotionContainer), "_player");
    private static readonly System.Reflection.MethodInfo PotionSetter =
        AccessTools.PropertySetter(typeof(NPotionHolder), "Potion");

    private const string ChaosPotionName = "EntropicBrew";

    [HarmonyPatch(typeof(NPotionContainer), "RemoveUsed")]
    [HarmonyPostfix]
    private static void RemoveUsedPostfix(NPotionContainer __instance)
    {
        CompactBelt(__instance);
        EnsureEntropicBrew(__instance);
    }

    [HarmonyPatch(typeof(NPotionContainer), "Discard")]
    [HarmonyPostfix]
    private static void DiscardPostfix(NPotionContainer __instance)
    {
        CompactBelt(__instance);
        EnsureEntropicBrew(__instance);
    }

    [HarmonyPatch(typeof(NPotionContainer), "Initialize")]
    [HarmonyPostfix]
    private static void InitializePostfix(NPotionContainer __instance)
    {
        EnsureEntropicBrew(__instance);
    }

    private static void CompactBelt(NPotionContainer container)
    {
        var holders = HoldersField?.GetValue(container) as List<NPotionHolder>;
        if (holders == null) return;

        for (int i = 0; i < holders.Count; i++)
        {
            if (holders[i] == null || !Godot.GodotObject.IsInstanceValid(holders[i]))
                continue;
            if (holders[i].HasPotion) continue;

            for (int j = i + 1; j < holders.Count; j++)
            {
                if (holders[j] == null || !Godot.GodotObject.IsInstanceValid(holders[j]))
                    continue;
                if (!holders[j].HasPotion) continue;

                var potion = holders[j].Potion;
                // 从源 holder 的场景树移除 NPotion（设 Potion=null 不会自动移）
                holders[j].RemoveChild(potion!);
                PotionSetter?.Invoke(holders[j], new object?[] { null });
                holders[i].AddPotion(potion!);
                break;
            }
        }
    }

    private static void EnsureEntropicBrew(NPotionContainer container)
    {
        var player = PlayerField?.GetValue(container) as Player;
        if (player == null) return;

        var holders = HoldersField?.GetValue(container) as List<NPotionHolder>;
        if (holders == null) return;

        // 已有混沌药水则跳过
        foreach (var h in holders)
        {
            if (!Godot.GodotObject.IsInstanceValid(h) || !h.HasPotion) continue;
            if (h.Potion!.Model.GetType().Name == ChaosPotionName)
                return;
        }

        // 找第一个空栏位
        int emptyIndex = -1;
        for (int i = 0; i < holders.Count; i++)
        {
            if (Godot.GodotObject.IsInstanceValid(holders[i]) && !holders[i].HasPotion)
            {
                emptyIndex = i;
                break;
            }
        }
        if (emptyIndex < 0) return; // 栏位已满

        // 从药水池中找混沌药水
        var options = PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>());
        var chaos = options.FirstOrDefault(p => p.GetType().Name == ChaosPotionName);
        if (chaos == null) return;

        var mutable = chaos.ToMutable();
        TaskHelper.RunSafely(PotionCmd.TryToProcure(mutable, player, emptyIndex));
    }
}
