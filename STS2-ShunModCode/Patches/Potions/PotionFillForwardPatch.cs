using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
/// </summary>

// ──────────────────────────────────────────
//  Patch 1: RemoveUsed
// ──────────────────────────────────────────
[HarmonyPatch(typeof(NPotionContainer), "RemoveUsed")]
internal static class PotionFillForward_RemoveUsed
{
    [HarmonyPostfix]
    private static void Postfix(NPotionContainer __instance) => PotionFillForwardLogic.OnPotionChanged(__instance);
}

// ──────────────────────────────────────────
//  Patch 2: Discard
// ──────────────────────────────────────────
[HarmonyPatch(typeof(NPotionContainer), "Discard")]
internal static class PotionFillForward_Discard
{
    [HarmonyPostfix]
    private static void Postfix(NPotionContainer __instance) => PotionFillForwardLogic.OnPotionChanged(__instance);
}

// ──────────────────────────────────────────
//  Patch 3: Initialize
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
    private static readonly FieldInfo? HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");
    private static readonly FieldInfo? PlayerField =
        AccessTools.Field(typeof(NPotionContainer), "_player");
    private static readonly MethodInfo? PotionSetter =
        AccessTools.PropertySetter(typeof(NPotionHolder), "Potion");
    private static readonly FieldInfo? PotionBackingField =
        AccessTools.Field(typeof(NPotionHolder), "<Potion>k__BackingField");
    private static readonly FieldInfo? EmptyIconField =
        AccessTools.Field(typeof(NPotionHolder), "_emptyIcon");

    private static bool _validated;
    private static bool _isProcessing;

    internal static async void OnPotionChanged(NPotionContainer container)
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

            var models = CollectModels(holders);
            await CompactIfNeeded(holders, player, models);
            await EnsureChaos(holders, player, models);

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

    /// <summary>收集所有 holder 中的药水 canonical 模型。</summary>
    private static List<PotionModel> CollectModels(List<NPotionHolder> holders)
    {
        var models = new List<PotionModel>();
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (!h.HasPotion || h.Potion == null) continue;
            var canonical = ModelDb.GetByIdOrNull<PotionModel>(h.Potion.Model.Id);
            if (canonical != null)
                models.Add(canonical);
        }
        return models;
    }

    /// <summary>检测间隙 → 清空重建（await 串行，避免竞态）。</summary>
    private static async Task CompactIfNeeded(List<NPotionHolder> holders, Player player, List<PotionModel> models)
    {
        if (models.Count == 0) return;

        // 检测间隙
        var found = 0;
        var hasGap = false;
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            if (h.HasPotion)
                found++;
            else if (found < models.Count)
            {
                hasGap = true;
                ShunLogger.Debug("药水填充/间隙检测", $"间隙 [{i}], 已有 {found}/{models.Count}");
                break;
            }
        }

        if (!hasGap)
        {
            ShunLogger.Debug("药水填充", $"无间隙, {models.Count} 个连续");
            return;
        }

        ShunLogger.Info("药水填充", $"间隙 → 重建 {models.Count} 个");

        // 清空
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h) || !h.HasPotion) continue;
            var potion = h.Potion;
            ClearPotion(h);
            if (potion != null && GodotObject.IsInstanceValid(potion))
            {
                h.RemoveChild(potion);
                potion.QueueFree();
            }
            RestoreEmptyIcon(h);
        }

        // 重建（串行 await）
        for (var i = 0; i < models.Count; i++)
        {
            ShunLogger.Debug("药水填充/重建", $"→ [{i}] {models[i].GetType().Name}");
            await PotionCmd.TryToProcure(models[i].ToMutable(), player, i);
        }

        ShunLogger.Info("药水填充", $"{models.Count} 个已前移");
    }

    /// <summary>保底混沌药水 — 基于 models 计算位置，不依赖 holder 状态（避免异步竞态）。</summary>
    private static async Task EnsureChaos(List<NPotionHolder> holders, Player player, List<PotionModel> models)
    {
        // 已有混沌药水
        if (models.Any(m => m is EntropicBrew))
        {
            ShunLogger.Debug("混沌药水", "已存在，跳过");
            return;
        }

        // 无空槽
        if (models.Count >= holders.Count)
        {
            ShunLogger.Debug("混沌药水", "无空槽，跳过");
            return;
        }

        // chaos 放在最后
        var chaosIdx = models.Count;
        ShunLogger.Info("混沌药水", $"→ 栏位 {chaosIdx}");

        var chaos = GetChaosModel(player);
        if (chaos == null) return;

        await PotionCmd.TryToProcure(chaos.ToMutable(), player, chaosIdx);
        models.Add(chaos); // 同步 models 避免重复补充
    }

    private static PotionModel? GetChaosModel(Player player)
    {
        try
        {
            var id = ModelDb.GetId(typeof(EntropicBrew));
            return ModelDb.GetByIdOrNull<PotionModel>(id);
        }
        catch (Exception ex)
        {
            ShunLogger.Warn("混沌药水", $"ModelDb: {ex.Message}");
        }

        var options = PotionFactory.GetPotionOptions(player, Array.Empty<PotionModel>());
        return options.FirstOrDefault(p => p is EntropicBrew);
    }

    // ── 日志 ──

    private static void LogPotionState(List<NPotionHolder> holders, string tag)
    {
        var parts = new List<string>();
        for (var i = 0; i < holders.Count; i++)
        {
            var h = holders[i];
            if (h == null || !GodotObject.IsInstanceValid(h))
                parts.Add($"[{i}]=无效");
            else if (!h.HasPotion || h.Potion == null || !GodotObject.IsInstanceValid(h.Potion))
                parts.Add($"[{i}]=空");
            else
                parts.Add($"[{i}]={h.Potion.Model.GetType().Name}");
        }
        ShunLogger.Info("药水填充/状态", $"{tag}: {string.Join(", ", parts)}");
    }

    // ── Helper ──

    private static void ClearPotion(NPotionHolder holder)
    {
        try
        {
            if (PotionSetter != null) { PotionSetter.Invoke(holder, [null]); return; }
            if (PotionBackingField != null) { PotionBackingField.SetValue(holder, null); return; }
            ShunLogger.Warn("药水填充", "PotionSetter / backing 均不可用");
        }
        catch (Exception ex) { ShunLogger.Error("药水填充/ClearPotion", ex); }
    }

    private static void RestoreEmptyIcon(NPotionHolder holder)
    {
        try
        {
            if (EmptyIconField?.GetValue(holder) is CanvasItem icon)
                icon.Modulate = Colors.White;
        }
        catch { /* 非关键 */ }
    }
}