using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core.Core.Helpers;

namespace ShunMod.Tweaks.Patches.Combat;

// ═══════════════════════════════════════════════════════════════════════════════
// 回合伤害预测 — 在角色头上显示本回合将要受到的伤害
//
// 场景：战斗开始后，玩家头顶实时显示「本回合所有敌人攻击意图总和 - 玩家格挡」
// 原理：Hook CombatManager 的回合开始/结束流程，计算所有敌人意图伤害，
//       减去玩家当前格挡，在头顶显示净伤害。
//
// 更新时机：回合开始、格挡变化、卡牌打出后刷新。
// 显示格式：红色数字 = 净伤害，绿色数字 = 溢出格挡
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance/__result 约定

/// <summary>
///     回合伤害预测 — 在角色头上显示将要受到的伤害。
/// </summary>
[HarmonyPatch]
public static class DamagePreviewPatch
{
    // ── 反射缓存 ──────────────────────────────────────────────────────────

    private static readonly Type? CreatureType = CreatureReflection.CreatureType;

    // 玩家头顶的 label（只显示一个）
    private static Label3D? _playerLabel;
    private static Tween? _playerTween;
    private static int _lastDisplayedNetDamage = int.MinValue;

    // ── 核心入口 ──────────────────────────────────────────────────────────

    /// <summary>
    ///     计算并更新所有角色头顶的伤害预测。
    ///     在战斗状态变化时调用（回合开始、格挡变化、卡牌打出）。
    /// </summary>
    public static void RefreshAll()
    {
        try
        {
            var combat = CombatManager.Instance;
            if (combat == null || !combat.IsInProgress) return;

            // 计算玩家将受到的净伤害
            var totalEnemyDamage = GetTotalEnemyAttackDamage(combat);
            var playerBlock = GetPlayerBlock(combat);
            var netDamage = Math.Max(0, totalEnemyDamage - playerBlock);

            // 更新玩家头顶显示
            UpdatePlayerLabel(netDamage, totalEnemyDamage, playerBlock);
        }
        catch (Exception ex)
        {
            Log.Error($"[伤害预测] RefreshAll 异常: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── 伤害计算 ──────────────────────────────────────────────────────────

    /// <summary>计算所有敌人本回合的攻击意图总伤害</summary>
    private static int GetTotalEnemyAttackDamage(object combatManager)
    {
        var totalDamage = 0;
        var enemies = GetEnemies(combatManager);
        if (enemies == null) return 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            var damage = GetEnemyIntentDamage(enemy);
            if (damage > 0)
                totalDamage += damage;
        }

        return totalDamage;
    }

    /// <summary>获取 CombatManager 上的敌人列表</summary>
    private static System.Collections.IEnumerable? GetEnemies(object combatManager)
    {
        var type = combatManager.GetType();

        // 尝试属性
        var prop = AccessTools.Property(type, "Enemies")
                   ?? AccessTools.Property(type, "Monsters")
                   ?? AccessTools.Property(type, "Creatures")
                   ?? AccessTools.Property(type, "AllCreatures");
        if (prop?.GetValue(combatManager) is System.Collections.IEnumerable enumerable)
            return enumerable;

        // 尝试方法
        var method = AccessTools.Method(type, "GetEnemies")
                     ?? AccessTools.Method(type, "GetMonsters")
                     ?? AccessTools.Method(type, "GetCreatures");
        if (method?.Invoke(combatManager, null) is System.Collections.IEnumerable result)
            return result;

        // 兜底：遍历所有 Creature 类型的属性
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (CreatureType == null || !CreatureType.IsAssignableFrom(p.PropertyType)) continue;
            // 单属性 → 跳过，我们要的是集合
        }

        // 找 IEnumerable<Creature> 类型的属性
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)) continue;
            var val = p.GetValue(combatManager);
            if (val is System.Collections.IEnumerable list)
            {
                // 检查第一个元素是否是 Creature 类型
                var enumerator = list.GetEnumerator();
                if (enumerator.MoveNext() && enumerator.Current != null)
                {
                    if (CreatureType?.IsInstanceOfType(enumerator.Current) == true)
                        return list;
                }
            }
        }

        return null;
    }

    /// <summary>获取敌人的意图伤害值</summary>
    private static int GetEnemyIntentDamage(object enemy)
    {
        var type = enemy.GetType();

        // 尝试 Intent.Damage 或 IntentDamage
        var intentProp = AccessTools.Property(type, "Intent");
        if (intentProp != null)
        {
            var intent = intentProp.GetValue(enemy);
            if (intent != null)
            {
                var intentType = intent.GetType();
                var damageProp = AccessTools.Property(intentType, "Damage")
                                 ?? AccessTools.Property(intentType, "Amount")
                                 ?? AccessTools.Property(intentType, "Value");
                if (damageProp?.GetValue(intent) is int dmg)
                    return dmg;
                if (damageProp?.GetValue(intent) is decimal dmgDec)
                    return (int)dmgDec;
            }
        }

        // 直接属性：IntentDamage
        var directProp = AccessTools.Property(type, "IntentDamage")
                         ?? AccessTools.Property(type, "AttackDamage")
                         ?? AccessTools.Property(type, "PlannedDamage");
        if (directProp?.GetValue(enemy) is int dmg2)
            return dmg2;

        // 方法：GetIntentDamage()
        var method = AccessTools.Method(type, "GetIntentDamage")
                     ?? AccessTools.Method(type, "GetAttackDamage")
                     ?? AccessTools.Method(type, "GetPlannedDamage");
        if (method?.Invoke(enemy, null) is int dmg3)
            return dmg3;

        // 方法：GetIntent() → 读 Damage
        var getIntentMethod = AccessTools.Method(type, "GetIntent");
        if (getIntentMethod?.Invoke(enemy, null) is { } intentObj)
        {
            var intentType = intentObj.GetType();
            var dProp = AccessTools.Property(intentType, "Damage")
                        ?? AccessTools.Property(intentType, "Amount");
            if (dProp?.GetValue(intentObj) is int d)
                return d;
        }

        return 0;
    }

    /// <summary>获取玩家当前格挡值</summary>
    private static int GetPlayerBlock(object combatManager)
    {
        var player = GetPlayer(combatManager);
        return player != null ? CreatureReflection.GetBlock(player) : 0;
    }

    /// <summary>获取 CombatManager 上的玩家对象</summary>
    private static object? GetPlayer(object combatManager)
    {
        var type = combatManager.GetType();

        var prop = AccessTools.Property(type, "Player")
                   ?? AccessTools.Property(type, "LocalPlayer")
                   ?? AccessTools.Property(type, "CurrentPlayer");
        if (prop?.GetValue(combatManager) is { } player && CreatureType?.IsInstanceOfType(player) == true)
            return player;

        return null;
    }

    // ── 头顶显示 ──────────────────────────────────────────────────────────

    /// <summary>更新玩家头顶的伤害预测标签</summary>
    private static void UpdatePlayerLabel(int netDamage, int totalEnemyDamage, int playerBlock)
    {
        // 无伤害时隐藏
        if (netDamage <= 0 && totalEnemyDamage <= 0)
        {
            ClearPlayerLabel();
            return;
        }

        // 值没变就不更新
        if (netDamage == _lastDisplayedNetDamage && _playerLabel != null
                                                 && GodotObject.IsInstanceIdValid(_playerLabel.GetInstanceId()))
            return;

        _lastDisplayedNetDamage = netDamage;

        // 找到玩家 Node
        var combat = CombatManager.Instance;
        if (combat == null) return;
        var player = GetPlayer(combat);
        if (player == null) return;

        var node = GetCreatureNode(player);
        if (node == null || !GodotObject.IsInstanceIdValid(node.GetInstanceId()))
        {
            node = FindCreatureNodeInScene(player);
            if (node == null) return;
        }

        // 清理旧 label
        ClearPlayerLabel();

        // 创建新 label
        _playerLabel = new Label3D();
        _playerLabel.Text = $"-{netDamage}";
        _playerLabel.FontSize = 52;
        _playerLabel.OutlineSize = 6;
        _playerLabel.OutlineModulate = Colors.Black;
        _playerLabel.PixelSize = 0.01f;
        _playerLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _playerLabel.NoDepthTest = true;

        // 颜色：净伤害>0 时红色，有格挡但挡不住时橙色
        if (netDamage > 0 && playerBlock > 0)
            _playerLabel.Modulate = new Color(1.0f, 0.6f, 0.0f); // 橙色：有格挡但不够
        else if (netDamage > 0)
            _playerLabel.Modulate = new Color(1.0f, 0.1f, 0.1f); // 红色：纯伤害
        else
            _playerLabel.Modulate = new Color(0.2f, 1.0f, 0.2f); // 绿色：全挡住

        node.AddChild(_playerLabel);
        _playerLabel.Owner = node.Owner ?? node;

        // 定位到头顶
        var aabb = node.GetAabb();
        var baseY = aabb.Size.Y > 0.01f ? aabb.Size.Y + 0.5f : 2.2f;
        _playerLabel.Position = new Vector3(0, baseY, 0);

        // 轻微脉动
        _playerTween = _playerLabel.CreateTween();
        _playerTween.SetLoops();
        _playerTween.SetParallel(true);
        _playerTween.TweenProperty(_playerLabel, "position:y", baseY + 0.2f, 1.0f)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        _playerTween.TweenProperty(_playerLabel, "position:y", baseY - 0.05f, 1.0f)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>清理玩家头顶的 label</summary>
    private static void ClearPlayerLabel()
    {
        if (_playerTween != null && GodotObject.IsInstanceIdValid(_playerTween.GetInstanceId()))
        {
            _playerTween.Kill();
            _playerTween = null;
        }

        if (_playerLabel != null && GodotObject.IsInstanceIdValid(_playerLabel.GetInstanceId()))
        {
            _playerLabel.QueueFree();
            _playerLabel = null;
        }

        _lastDisplayedNetDamage = int.MinValue;
    }

    // ── Node 查找 ─────────────────────────────────────────────────────────

    private static Node3D? GetCreatureNode(object creature)
    {
        var type = creature.GetType();
        var prop = AccessTools.Property(type, "Node")
                   ?? AccessTools.Property(type, "CreatureNode")
                   ?? AccessTools.Property(type, "ViewNode")
                   ?? AccessTools.Property(type, "ModelNode");

        if (prop?.GetValue(creature) is Node3D node)
            return node;

        if (creature is Node3D self)
            return self;

        return null;
    }

    private static Node3D? FindCreatureNodeInScene(object creature)
    {
        try
        {
            var combatManager = CombatManager.Instance;
            if (combatManager == null) return null;

            var idProp = AccessTools.Property(creature.GetType(), "CreatureId")
                         ?? AccessTools.Property(creature.GetType(), "Id")
                         ?? AccessTools.Property(creature.GetType(), "Name");
            var creatureId = idProp?.GetValue(creature)?.ToString();
            if (creatureId == null) return null;
            if (creatureId == "Player" || creatureId == "0") return null; // 太宽泛，跳过

            var tree = Engine.GetMainLoop();
            if (tree is not SceneTree sceneTree) return null;

            return FindFirstNode3DRecursive(sceneTree.Root, creatureId);
        }
        catch
        {
            return null;
        }
    }

    private static Node3D? FindFirstNode3DRecursive(Node node, string creatureId)
    {
        if (node is Node3D n3d && n3d.Name.ToString()
                .Contains(creatureId, StringComparison.OrdinalIgnoreCase))
            return n3d;

        foreach (var child in node.GetChildren())
        {
            var result = FindFirstNode3DRecursive(child, creatureId);
            if (result != null) return result;
        }

        return null;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// 回合开始/结束 Hook — 触发伤害预测刷新
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance 约定

/// <summary>Hook CombatManager 的回合开始方法，刷新伤害预测</summary>
[HarmonyPatch]
public static class DamagePreviewTurnStartPatch
{
    private static readonly MethodInfo? Target = FindTarget();

    private static MethodInfo? FindTarget()
    {
        var combatType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (combatType == null) return null;

        // 尝试多个可能的回合开始方法名
        foreach (var name in new[] { "StartTurn", "OnTurnStart", "BeginTurn", "StartPlayerTurn", "StartEnemyTurn" })
        {
            var method = AccessTools.DeclaredMethod(combatType, name);
            if (method != null) return method;
        }

        return null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static MethodInfo? TargetMethod() => Target;

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static bool Prepare()
    {
        if (Target != null)
        {
            Log.Info($"[伤害预测] 已 Hook CombatManager.{Target.Name}");
            return true;
        }
        Log.Error("[伤害预测] 未找到 CombatManager 回合方法，跳过回合开始刷新");
        return false;
    }

    private static void Postfix() => DamagePreviewPatch.RefreshAll();
}

/// <summary>Hook CombatManager 的卡牌打出方法，刷新伤害预测</summary>
[HarmonyPatch]
public static class DamagePreviewCardPlayedPatch
{
    private static readonly MethodInfo? Target = FindTarget();

    private static MethodInfo? FindTarget()
    {
        var combatType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (combatType == null) return null;

        foreach (var name in new[] { "PlayCard", "OnCardPlayed", "ExecuteCard", "ResolveCard" })
        {
            var method = AccessTools.DeclaredMethod(combatType, name);
            if (method != null) return method;
        }

        return null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static MethodInfo? TargetMethod() => Target;

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static bool Prepare()
    {
        if (Target != null)
        {
            Log.Info($"[伤害预测] 已 Hook CombatManager.{Target.Name}");
            return true;
        }
        Log.Error("[伤害预测] 未找到 CombatManager 卡牌方法，跳过卡牌打出刷新");
        return false;
    }

    private static void Postfix() => DamagePreviewPatch.RefreshAll();
}