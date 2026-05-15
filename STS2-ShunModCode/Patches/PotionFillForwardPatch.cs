using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 药水填充前移：使用/丢弃药水后，后方药水自动向前填充空位。
/// 例：5栏位用完3号 → 4号→3号, 5号→4号。
/// </summary>
[HarmonyPatchCategory("Gameplay")]
internal static class PotionFillForwardPatch
{
    private static readonly System.Reflection.FieldInfo HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");

    private static readonly System.Reflection.MethodInfo PotionSetter =
        AccessTools.PropertySetter(typeof(NPotionHolder), "Potion");

    [HarmonyPatch(typeof(NPotionContainer), "RemoveUsed")]
    [HarmonyPostfix]
    private static void RemoveUsedPostfix(NPotionContainer __instance)
    {
        CompactBelt(__instance);
    }

    [HarmonyPatch(typeof(NPotionContainer), "Discard")]
    [HarmonyPostfix]
    private static void DiscardPostfix(NPotionContainer __instance)
    {
        CompactBelt(__instance);
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

            // 找后方第一个有药水的栏位
            for (int j = i + 1; j < holders.Count; j++)
            {
                if (holders[j] == null || !Godot.GodotObject.IsInstanceValid(holders[j]))
                    continue;
                if (!holders[j].HasPotion) continue;

                var potion = holders[j].Potion;
                // 从源栏位移除（Potion 设 null 后 AddPotion 会 AddChild，自动移父）
                PotionSetter?.Invoke(holders[j], new object?[] { null });
                // 添加到目标栏位
                holders[i].AddPotion(potion!);
                break;
            }
        }
    }
}
