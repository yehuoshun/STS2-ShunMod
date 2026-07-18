using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShunMod.Tweaks.Patches.Combat;

// ═══════════════════════════════════════════════════════════════════════════════
// 回合伤害预测
//
// 玩家头顶：所有敌人意图伤害总和（穿透所有加成）− 格挡 = 净伤害
// 敌人头顶：意图伤害，多段 "8×3 (24)"
//
// 更新时机：回合开始、卡牌打出
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
public static class DamagePreview
{
    // 玩家头顶 label
    private static Label? _playerLabel;
    private static Tween? _playerTween;
    private static int _lastNetDamage = int.MinValue;

    // 敌人头顶 label 缓存
    private static readonly Dictionary<uint, Label> EnemyLabels = new();

    // ── 刷新入口 ──────────────────────────────────────────────────────────

    public static void Refresh()
    {
        try
        {
            var combat = CombatManager.Instance;
            if (!combat.IsInProgress) return;
            var state = CombatManager.Instance.DebugOnlyGetState();
            if (state == null) return;

            // 玩家
            foreach (var pc in state.PlayerCreatures)
            {
                var total = state.Enemies
                    .Where(e => !e.IsDead && e.IsPrimaryEnemy)
                    .Sum(GetEnemyTotalDamage);

                // 穿透所有加成（易伤、力量等）
                var actual = Hook.ModifyDamage(state.RunState, state, pc, null, total,
                    ValueProp.Move, null, ModifyDamageHookType.All, CardPreviewMode.None, out _);

                var net = Math.Max(0, (int)actual - pc.Block);
                UpdatePlayerLabel(pc, net);
            }

            // 敌人
            foreach (var enemy in state.Enemies)
            {
                if (enemy.IsDead) continue;
                UpdateEnemyLabel(enemy);
            }

            CleanupDeadEnemies(state);
        }
        catch (ObjectDisposedException)
        {
            // 战斗场景清理时 Label 已被销毁，UI 同步失败不影响游戏
        }
    }

    // ── 敌人意图 ──────────────────────────────────────────────────────────

    private static int GetEnemyTotalDamage(Creature enemy)
    {
        if (enemy.Monster?.NextMove == null) return 0;
        var total = 0;
        foreach (var intent in enemy.Monster.NextMove.Intents)
            if (intent is AttackIntent atk)
            {
                var targets = enemy.CombatState?.Enemies ?? [];
                total += atk.GetTotalDamage(targets, enemy);
            }

        return total;
    }

    private static (int perHit, int repeats) GetEnemyIntentInfo(Creature enemy)
    {
        if (enemy.Monster?.NextMove == null) return (0, 0);
        foreach (var intent in enemy.Monster.NextMove.Intents)
            if (intent is AttackIntent atk)
            {
                var targets = enemy.CombatState?.Enemies ?? [];
                return (atk.GetSingleDamage(targets, enemy), atk.Repeats);
            }

        return (0, 0);
    }

    // ── 玩家头顶 ──────────────────────────────────────────────────────────

    private static void UpdatePlayerLabel(Creature player, int netDamage)
    {
        if (netDamage <= 0)
        {
            ClearPlayerLabel();
            return;
        }

        if (netDamage == _lastNetDamage && IsValid(_playerLabel))
            return;

        _lastNetDamage = netDamage;

        var node = player.GetCreatureNode();
        if (!IsValid(node)) return;

        ClearPlayerLabel();

        _playerLabel = new Label();
        _playerLabel.Text = $"-{netDamage}";
        _playerLabel.AddThemeFontSizeOverride("font_size", 28);
        _playerLabel.AddThemeColorOverride("font_color", Colors.Red);
        _playerLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);

        node.AddChild(_playerLabel);
        _playerLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _playerLabel.Position = new Vector2(0, -60);

        _playerTween = _playerLabel.CreateTween();
        _playerTween.SetLoops();
        _playerTween.SetParallel();
        _playerTween.TweenProperty(_playerLabel, "position:y", -55, 1.0f)
            .SetTrans(Tween.TransitionType.Sine);
        _playerTween.TweenProperty(_playerLabel, "position:y", -65, 1.0f)
            .SetTrans(Tween.TransitionType.Sine);
    }

    private static bool IsValid(GodotObject? obj)
    {
        if (obj == null) return false;
        try
        {
            return GodotObject.IsInstanceIdValid(obj.GetInstanceId());
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static void ClearPlayerLabel()
    {
        if (IsValid(_playerTween))
            _playerTween!.Kill();
        if (IsValid(_playerLabel))
            _playerLabel!.QueueFree();
        _playerLabel = null;
        _playerTween = null;
        _lastNetDamage = int.MinValue;
    }

    // ── 敌人头顶 ──────────────────────────────────────────────────────────

    private static void UpdateEnemyLabel(Creature enemy)
    {
        var (perHit, repeats) = GetEnemyIntentInfo(enemy);
        if (perHit <= 0)
        {
            RemoveEnemyLabel(enemy);
            return;
        }

        var total = perHit * Math.Max(1, repeats);
        var text = repeats > 1 ? $"{perHit}×{repeats} ({total})" : total.ToString();

        if (enemy.CombatId == null) return;
        var cid = enemy.CombatId.Value;

        if (EnemyLabels.TryGetValue(cid, out var existing) && IsValid(existing)
                                                           && existing.Text == text)
            return;

        RemoveEnemyLabel(enemy);

        var node = enemy.GetCreatureNode();
        if (!IsValid(node)) return;

        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", Colors.Red);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);

        node.AddChild(label);
        label.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        label.Position = new Vector2(0, -40);

        EnemyLabels[cid] = label;
    }

    private static void RemoveEnemyLabel(Creature enemy)
    {
        if (enemy.CombatId == null) return;
        var cid = enemy.CombatId.Value;
        if (EnemyLabels.Remove(cid, out var label) && IsValid(label))
            label.QueueFree();
    }

    private static void CleanupDeadEnemies(CombatState state)
    {
        var alive = state.Enemies.Select(e => e.CombatId).ToHashSet();
        foreach (var key in EnemyLabels.Keys.ToList())
            if (!alive.Contains(key))
                RemoveEnemyLabelByCid(key);
    }

    private static void RemoveEnemyLabelByCid(uint cid)
    {
        if (EnemyLabels.Remove(cid, out var label) && GodotObject.IsInstanceIdValid(label.GetInstanceId()))
            label.QueueFree();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Hook 点
// ═══════════════════════════════════════════════════════════════════════════════

[HarmonyPatch]
public static class DamagePreviewTurnStart
{
    private static readonly MethodInfo? Target =
        AccessTools.DeclaredMethod(typeof(CombatManager), "StartTurn", [typeof(Func<Task>)]);

    private static MethodInfo? TargetMethod()
    {
        return Target;
    }

    private static bool Prepare()
    {
        if (Target != null)
        {
            Log.Info("[伤害预测] Hook: CombatManager.StartTurn");
            return true;
        }

        Log.Error("[伤害预测] 未找到 CombatManager.StartTurn");
        return false;
    }

    private static void Postfix()
    {
        DamagePreview.Refresh();
    }
}

[HarmonyPatch]
public static class DamagePreviewCardPlayed
{
    private static readonly MethodInfo? Target =
        AccessTools.DeclaredMethod(typeof(CreatureCmd), "Damage",
        [
            typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp),
            typeof(Creature), typeof(CardModel)
        ]);

    private static MethodInfo? TargetMethod()
    {
        return Target;
    }

    private static bool Prepare()
    {
        if (Target != null)
        {
            Log.Info("[伤害预测] Hook: CreatureCmd.Damage");
            return true;
        }

        Log.Error("[伤害预测] 未找到 CreatureCmd.Damage 重载");
        return false;
    }

    private static void Postfix()
    {
        DamagePreview.Refresh();
    }
}