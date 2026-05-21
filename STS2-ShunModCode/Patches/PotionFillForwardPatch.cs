using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes.Potions;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     药水填充前移 + 混沌药水保底。
///     使用/丢弃后后方药水向前填充，若无混沌药水则自动补充。
///
///     三个独立类分别 Patch RemoveUsed / Discard / Initialize，
///     共用 PotionFillForwardLogic 处理实际逻辑。
/// </summary>

// ──────────────────────────────────────────
//  Patch 1: RemoveUsed（使用药水后）
// ──────────────────────────────────────────
[HarmonyPatch(typeof(NPotionContainer), "RemoveUsed")]
internal static class PotionFillForward_RemoveUsed
{
    [HarmonyPostfix]
    private static void Postfix(NPotionContainer __instance) => PotionFillForwardLogic.OnPotionChanged(__instance);
}

// ──────────────────────────────────────────
//  Patch 2: Discard（丢弃药水后）
// ──────────────────────────────────────────
[HarmonyPatch(typeof(NPotionContainer), "Discard")]
internal static class PotionFillForward_Discard
{
    [HarmonyPostfix]
    private static void Postfix(NPotionContainer __instance) => PotionFillForwardLogic.OnPotionChanged(__instance);
}

// ──────────────────────────────────────────
//  Patch 3: Initialize（开局初始化）
// ──────────────────────────────────────────
[HarmonyPatch(typeof(NPotionContainer), "Initialize")]
internal static class PotionFillForward_Initialize
{
    [HarmonyPostfix]
    private static void Postfix(NPotionContainer __instance) => PotionFillForwardLogic.OnPotionChanged(__instance);
}

// ═══════════════════════════════════════════════
//  共享逻辑
// ═══════════════════════════════════════════════
internal static class PotionFillForwardLogic
{
    // ── NPotionContainer ──
    private static readonly FieldInfo? HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");

    private static readonly FieldInfo? PlayerField =
        AccessTools.Field(typeof(NPotionContainer), "_player");

    // ── NPotionHolder ──
    private static readonly MethodInfo? PotionSetter =
        AccessTools.PropertySetter(typeof(NPotionHolder), "Potion");

    private static readonly FieldInfo? PotionBackingField =
        AccessTools.Field(typeof(NPotionHolder), "<Potion>k__BackingField");

    private static readonly FieldInfo? EmptyIconField =
        AccessTools.Field(typeof(NPotionHolder), "_emptyIcon");

    private static bool _validated;

    internal static void OnPotionChanged(NPotionContainer container)
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

    private static void CompactIfNeeded(List<NPotionHolder> holders, Player player)
    {
        // 收集现有药水的 canonical 模型（不直接用 h.Potion.Model，
        // 因为它可能是之前 Compact 产生的 mutable clone，再次 ToMutable 会炸）
        var models = new List<PotionModel>();
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (!h.HasPotion || h.Potion == null) continue;
            // 从 ModelDb 取 canonical 版本，避免 mutable 链
            var canonical = ModelDb.GetByIdOrNull<PotionModel>(h.Potion.Model.Id);
            if (canonical != null)
                models.Add(canonical);
        }

        if (models.Count == 0) return;

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

        if (!needCompact)
        {
            ShunLogger.Debug("药水填充", $"无间隙，{models.Count} 个药水已连续");
            return;
        }

        ShunLogger.Info("药水填充", $"检测到间隙 → 整理 {models.Count} 个药水");

        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion) continue;

            var potion = h.Potion;
            ClearPotion(h);

            if (potion != null && GodotObject.IsInstanceValid(potion))
            {
                h.RemoveChildSafely(potion);
                potion.QueueFreeSafely();
            }

            RestoreEmptyIcon(h);
        }

        for (var i = 0; i < models.Count; i++)
        {
            var index = i;
            // 从 canonical 模型创建新鲜 mutable clone，避免二次 ToMutable 导致的 MutableModelException
            var mutable = models[i].ToMutable();
            TaskHelper.RunSafely(PotionCmd.TryToProcure(mutable, player, index));
        }

        ShunLogger.Info("药水填充", $"{models.Count} 个药水已前移");
    }

    private static void EnsureEntropicBrew(List<NPotionHolder> holders, Player player)
    {
        foreach (var h in holders)
        {
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion || h.Potion == null)
                continue;
            if (h.Potion.Model is EntropicBrew)
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

        // 直接用 ModelDb + 类型 — 绕过 PotionFactory 池限制
        PotionModel? chaos = null;
        try
        {
            var id = ModelDb.GetId(typeof(EntropicBrew));
            chaos = ModelDb.GetByIdOrNull<PotionModel>(id);
        }
        catch (Exception ex)
        {
            ShunLogger.Warn("混沌药水", $"ModelDb 失败: {ex.Message}，回退 PotionFactory");
        }

        // 回退：PotionFactory
        if (chaos == null)
        {
            var options = PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>());
            chaos = options.FirstOrDefault(p => p is EntropicBrew);
        }

        if (chaos == null)
        {
            ShunLogger.Warn("混沌药水", "无法获取 EntropicBrew");
            return;
        }

        ShunLogger.Info("混沌药水", $"→ 栏位 {emptyIdx}");
        var mutable = chaos.ToMutable();
        TaskHelper.RunSafely(PotionCmd.TryToProcure(mutable, player, emptyIdx));
    }

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