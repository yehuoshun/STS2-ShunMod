using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     药水填充前移 + 混沌药水保底。
///     使用/丢弃后后方药水向前填充，若无混沌药水则自动补充。
/// </summary>
[HarmonyPatchCategory("Gameplay")]
internal static class PotionFillForwardPatch
{
    private const string ChaosPotionName = "EntropicBrew";

    // ── NPotionContainer ──
    private static readonly FieldInfo HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");

    private static readonly FieldInfo PlayerField =
        AccessTools.Field(typeof(NPotionContainer), "_player");

    // ── NPotionHolder ──
    private static readonly FieldInfo EmptyIconField =
        AccessTools.Field(typeof(NPotionHolder), "_emptyIcon");

    // ═══════════════════════════════════════════════
    //  Harmony Patches
    // ═══════════════════════════════════════════════

    [HarmonyPatch(typeof(NPotionContainer), "RemoveUsed")]
    [HarmonyPostfix]
    private static void OnRemoveUsed(NPotionContainer __instance) => OnPotionChanged(__instance);

    [HarmonyPatch(typeof(NPotionContainer), "Discard")]
    [HarmonyPostfix]
    private static void OnDiscard(NPotionContainer __instance) => OnPotionChanged(__instance);

    [HarmonyPatch(typeof(NPotionContainer), "Initialize")]
    [HarmonyPostfix]
    private static void OnInitialize(NPotionContainer __instance) => OnPotionChanged(__instance);

    // ═══════════════════════════════════════════════
    //  核心逻辑
    // ═══════════════════════════════════════════════

    private static void OnPotionChanged(NPotionContainer container)
    {
        try
        {
            var holders = HoldersField?.GetValue(container) as List<NPotionHolder>;
            var player = PlayerField?.GetValue(container) as Player;
            if (holders == null || player == null)
            {
                ShunLogger.Warn("药水填充", "反射取 holders / player 失败，补丁未生效");
                return;
            }

            CompactIfNeeded(container, holders, player);
            EnsureEntropicBrew(container, holders, player);
        }
        catch (Exception ex)
        {
            ShunLogger.Error("药水填充", ex);
        }
    }

    /// <summary>
    ///     收集所有药水模型 → 清除所有栏位 → 重新从左获取。
    ///     不使用 RemoveChild+AddPotion 搬节点，避免 Godot 场景树竞态。
    /// </summary>
    private static void CompactIfNeeded(
        NPotionContainer container,
        List<NPotionHolder> holders,
        Player player)
    {
        // 1. 收集药水模型（从左到右，跳过空栏）
        var models = new List<PotionModel>();
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (!h.HasPotion || h.Potion == null) continue;
            models.Add(h.Potion.Model);
        }

        if (models.Count == 0) return; // 空带，只靠 EnsureEntropicBrew

        // 2. 检测是否有间隙（空栏位出现在非空栏位之前）
        var found = 0;
        var needCompact = false;
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (h.HasPotion)
            {
                found++;
            }
            else if (found < models.Count)
            {
                needCompact = true;
                break;
            }
        }

        if (!needCompact) return;

        ShunLogger.Info("药水填充", $"检测到间隙 → 整理 {models.Count} 个药水");

        // 3. 清除所有 holder（Potion setter 负责内部清理，无需手动 RemoveChild）
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion) continue;
            h.Potion = null;
            RestoreEmptyIcon(h);
        }

        // 4. 通过 PotionCmd 从左到右重新获取（不搬节点，创建新实例）
        for (var i = 0; i < models.Count; i++)
        {
            var mutable = models[i].ToMutable();
            TaskHelper.RunSafely(PotionCmd.TryToProcure(mutable, player, i));
        }

        ShunLogger.Info("药水填充", $"{models.Count} 个药水已前移");
    }

    /// <summary>
    ///     确保药水带中存在至少一瓶混沌药水（EntropicBrew）。
    ///     已有时跳过；无时空栏位自动补。
    /// </summary>
    private static void EnsureEntropicBrew(
        NPotionContainer container,
        List<NPotionHolder> holders,
        Player player)
    {
        // 已有则跳过
        foreach (var h in holders)
        {
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion || h.Potion == null)
                continue;
            if (h.Potion.Model.GetType().Name == ChaosPotionName)
                return;
        }

        // 找第一个空栏位
        var emptyIdx = -1;
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h) || h.HasPotion) continue;
            emptyIdx = i;
            break;
        }

        if (emptyIdx < 0) return; // 满了

        var options = PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>());
        var chaos = options.FirstOrDefault(p => p.GetType().Name == ChaosPotionName);
        if (chaos == null)
        {
            ShunLogger.Warn("混沌药水", "药水池中无 EntropicBrew");
            return;
        }

        ShunLogger.Info("混沌药水", $"→ 栏位 {emptyIdx}");
        TaskHelper.RunSafely(PotionCmd.TryToProcure(chaos.ToMutable(), player, emptyIdx));
    }

    // ═══════════════════════════════════════════════
    //  工具方法
    // ═══════════════════════════════════════════════

    private static void RestoreEmptyIcon(NPotionHolder holder)
    {
        try
        {
            if (EmptyIconField?.GetValue(holder) is CanvasItem icon)
                icon.Modulate = Colors.White;
        }
        catch
        {
            // 非关键
        }
    }
}