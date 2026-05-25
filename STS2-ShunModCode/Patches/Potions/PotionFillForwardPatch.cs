using System;
using System.Collections.Generic;
using System.Linq;
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
    private static bool _isProcessing;

    internal static void OnPotionChanged(NPotionContainer container)
    {
        if (_isProcessing) return;
        _isProcessing = true;
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

            LogPotionState(holders, "变动前");

            CompactIfNeeded(holders, player);
            EnsureEntropicBrew(holders, player);

            LogPotionState(holders, "变动后");
        }
        catch (Exception ex)
        {
            ShunLogger.Error("药水填充", ex);
        }
        finally
        {
            _isProcessing = false;
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
    ///     检测间隙 → 移动现有药水节点填充（不重建，避免异步竞态）。
    /// </summary>
    private static void CompactIfNeeded(List<NPotionHolder> holders, Player player)
    {
        // 1. 检测是否有间隙（空槽位后面还有药水）
        var hasGap = false;
        var foundPotion = false;
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (h.HasPotion)
                foundPotion = true;
            else if (foundPotion)
            {
                hasGap = true;
                ShunLogger.Debug("药水填充/间隙检测", $"间隙位置: [{i}] 为空, 但前面有药水");
                break;
            }
        }

        if (!hasGap)
        {
            ShunLogger.Debug("药水填充", "无间隙，跳过整理");
            return;
        }

        // 2. 收集所有药水节点，从旧 holder 清除
        var potionNodes = new List<NPotion>();
        var oldIndices = new List<int>();
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (!h.HasPotion || h.Potion == null) continue;

            potionNodes.Add(h.Potion);
            oldIndices.Add(i);
            ClearPotion(h);
            RestoreEmptyIcon(h);
            // 不 QueueFree — 仅移动，不销毁
        }

        ShunLogger.Info("药水填充", $"检测到间隙 → 前移 {potionNodes.Count} 个药水 " +
            $"(旧位置: [{string.Join(",", oldIndices)}])");

        // 3. 按序放入前 N 个 holder
        for (var i = 0; i < potionNodes.Count; i++)
        {
            var h = holders[i];
            h.AddChildSafely(potionNodes[i]);
            SetPotion(h, potionNodes[i]);
            ShunLogger.Debug("药水填充/移动",
                $"{potionNodes[i].Model.GetType().Name} [{oldIndices[i]}] → [{i}]");
        }

        ShunLogger.Info("药水填充", $"{potionNodes.Count} 个药水已前移");
    }

    private static void EnsureEntropicBrew(List<NPotionHolder> holders, Player player)
    {
        // 检查是否已有混沌药水
        foreach (var h in holders)
        {
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion || h.Potion == null)
                continue;
            if (h.Potion.Model is EntropicBrew)
            {
                ShunLogger.Debug("混沌药水", "已存在，跳过补充");
                return;
            }
        }

        // 找第一个空槽
        var emptyIdx = -1;
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h) || h.HasPotion) continue;
            emptyIdx = i;
            break;
        }

        if (emptyIdx < 0)
        {
            ShunLogger.Debug("混沌药水", "无空槽位，跳过补充");
            return;
        }

        // 获取 EntropicBrew
        PotionModel? chaos = null;
        try
        {
            var id = ModelDb.GetId(typeof(EntropicBrew));
            chaos = ModelDb.GetByIdOrNull<PotionModel>(id);
            ShunLogger.Debug("混沌药水", $"ModelDb 获取: {(chaos != null ? "成功" : "失败")}");
        }
        catch (Exception ex)
        {
            ShunLogger.Warn("混沌药水", $"ModelDb 失败: {ex.Message}，回退 PotionFactory");
        }

        if (chaos == null)
        {
            var options = PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>());
            chaos = options.FirstOrDefault(p => p is EntropicBrew);
            ShunLogger.Debug("混沌药水", $"PotionFactory 回退: {(chaos != null ? "成功" : "失败")}");
        }

        if (chaos == null)
        {
            ShunLogger.Warn("混沌药水", "无法获取 EntropicBrew");
            return;
        }

        ShunLogger.Info("混沌药水", $"→ 栏位 {emptyIdx} (共 {holders.Count} 栏位, 已有 {holders.Count(h => h.HasPotion)} 个药水)");
        var mutable = chaos.ToMutable();
        TaskHelper.RunSafely(PotionCmd.TryToProcure(mutable, player, emptyIdx));
    }

    // ── 药水状态日志 ──

    private static void LogPotionState(List<NPotionHolder> holders, string tag)
    {
        var parts = new List<string>();
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h))
            {
                parts.Add($"[{i}]=无效");
                continue;
            }
            if (!h.HasPotion || h.Potion == null || !GodotObject.IsInstanceValid(h.Potion))
            {
                parts.Add($"[{i}]=空");
                continue;
            }
            parts.Add($"[{i}]={h.Potion.Model.GetType().Name}");
        }

        ShunLogger.Info("药水填充/状态", $"{tag}: {string.Join(", ", parts)}");
    }

    // ── Helper ──

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

    private static void SetPotion(NPotionHolder holder, NPotion potion)
    {
        try
        {
            PotionSetter?.Invoke(holder, [potion]);
        }
        catch (Exception ex)
        {
            ShunLogger.Error("药水填充/SetPotion", ex);
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