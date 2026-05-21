using System.Reflection;
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
///
///     实现机制：不搬 Godot 节点（避免场景树竞态），
///     而是收集模型 → 清除栏位 → 通过 PotionCmd.TryToProcure 重新获取。
/// </summary>
[HarmonyPatchCategory("Gameplay")]
internal static class PotionFillForwardPatch
{
    private const string ChaosPotionName = "EntropicBrew";

    // ── NPotionContainer ──
    private static readonly FieldInfo? HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");

    private static readonly FieldInfo? PlayerField =
        AccessTools.Field(typeof(NPotionContainer), "_player");

    // ── NPotionHolder ──
    // Potion setter 是 private { get; private set; }，必须用反射；
    // 回退到 backing field 以防 PropertySetter 找不到
    private static readonly MethodInfo? PotionSetter =
        AccessTools.PropertySetter(typeof(NPotionHolder), "Potion");

    private static readonly FieldInfo? PotionBackingField =
        AccessTools.Field(typeof(NPotionHolder), "<Potion>k__BackingField");

    private static readonly FieldInfo? EmptyIconField =
        AccessTools.Field(typeof(NPotionHolder), "_emptyIcon");

    private static bool _validated;

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
            ValidateReflection();

            var holders = HoldersField?.GetValue(container) as List<NPotionHolder>;
            var player = PlayerField?.GetValue(container) as Player;
            if (holders == null || player == null)
            {
                ShunLogger.Warn("药水填充", "反射取 holders / player 失败");
                return;
            }

            CompactIfNeeded(holders, player);
            EnsureEntropicBrew(holders, player);
        }
        catch (Exception ex)
        {
            ShunLogger.Error("药水填充", ex);
        }
    }

    private static void ValidateReflection()
    {
        if (_validated) return;
        _validated = true;

        ShunLogger.Info("药水填充/反射",
            $"HoldersField={HoldersField != null}, PlayerField={PlayerField != null}, " +
            $"PotionSetter={PotionSetter != null}, PotionBacking={PotionBackingField != null}");
    }

    /// <summary>
    ///     收集所有药水模型 → 清除所有栏位 → 重新从左获取。
    /// </summary>
    private static void CompactIfNeeded(List<NPotionHolder> holders, Player player)
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

        if (models.Count == 0) return;

        // 2. 检测是否有间隙
        var found = 0;
        var needCompact = false;
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (h.HasPotion)
                found++;
            else if (found < models.Count)
            { needCompact = true; break; }
        }

        if (!needCompact) return;

        ShunLogger.Info("药水填充", $"检测到间隙 → 整理 {models.Count} 个药水");

        // 3. 清除所有 holder 的药水
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion) continue;

            var potion = h.Potion;
            ClearPotion(h);

            // 清理孤立的 NPotion 节点
            if (potion != null && GodotObject.IsInstanceValid(potion))
            {
                h.RemoveChildSafely(potion);
                potion.QueueFreeSafely();
            }

            RestoreEmptyIcon(h);
        }

        // 4. 从左到右重新获取
        for (var i = 0; i < models.Count; i++)
        {
            var index = i;
            var mutable = models[i].ToMutable();
            TaskHelper.RunSafely(PotionCmd.TryToProcure(mutable, player, index));
        }

        ShunLogger.Info("药水填充", $"{models.Count} 个药水已前移");
    }

    /// <summary>
    ///     确保药水带中存在至少一瓶混沌药水（EntropicBrew）。
    /// </summary>
    private static void EnsureEntropicBrew(List<NPotionHolder> holders, Player player)
    {
        foreach (var h in holders)
        {
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion || h.Potion == null)
                continue;
            if (h.Potion.Model.GetType().Name == ChaosPotionName)
                return;
        }

        var emptyIdx = -1;
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h) || h.HasPotion) continue;
            emptyIdx = i;
            break;
        }

        if (emptyIdx < 0) return;

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

    /// <summary>
    ///     清除 holder 中的药水引用。
    ///     优先用 PropertySetter 反射，回退到 backing field。
    /// </summary>
    private static void ClearPotion(NPotionHolder holder)
    {
        try
        {
            if (PotionSetter != null)
            {
                PotionSetter.Invoke(holder, [null]);
                return;
            }

            if (PotionBackingField != null)
            {
                PotionBackingField.SetValue(holder, null);
                return;
            }

            ShunLogger.Warn("药水填充", "PotionSetter 和 backing field 均不可用");
        }
        catch (Exception ex)
        {
            ShunLogger.Error("药水填充/ClearPotion", ex);
        }
    }

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