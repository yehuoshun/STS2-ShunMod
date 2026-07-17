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
// 玩家头顶：净伤害 = 所有敌人意图伤害总和（穿透易伤/虚弱等加成）− 格挡
// 敌人头顶：意图伤害，多段格式 "8×3 (24)"
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
    private static readonly MethodInfo? ModifyHpLostMethod = FindModifyHpLost();

    // 玩家头顶 label
    private static Label3D? _playerLabel;
    private static Tween? _playerTween;
    private static int _lastPlayerNetDamage = int.MinValue;

    // 敌人头顶 label 缓存 <creatureHash, label>
    private static readonly ConcurrentDictionary<int, Label3D> EnemyLabels = new();

    // ── 反射初始化 ────────────────────────────────────────────────────────

    private static MethodInfo? FindModifyHpLost()
    {
        if (CreatureType == null) return null;
        foreach (var name in new[] { "ModifyHpLost", "CalculateDamageTaken", "ApplyDamageModifiers", "GetFinalDamage" })
        {
            foreach (var m in CreatureType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != name) continue;
                var pars = m.GetParameters();
                if (pars.Length > 0 && pars[0].ParameterType == typeof(decimal))
                    return m;
            }
        }
        return null;
    }

    // ── 核心入口 ──────────────────────────────────────────────────────────

    public static void RefreshAll()
    {
        try
        {
            var combat = CombatManager.Instance;
            if (combat == null || !combat.IsInProgress) return;

            var player = GetPlayer(combat);
            if (player == null) return;

            // 1. 更新玩家头顶：总伤害 − 格挡
            var totalActualDamage = GetTotalActualDamageToPlayer(combat, player);
            var playerBlock = CreatureReflection.GetBlock(player);
            var netDamage = Math.Max(0, totalActualDamage - playerBlock);
            UpdatePlayerLabel(netDamage, totalActualDamage, playerBlock, player);

            // 2. 更新敌人头顶：意图伤害
            UpdateEnemyLabels(combat);
        }
        catch (Exception ex)
        {
            Log.Error($"[伤害预测] RefreshAll 异常: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── 玩家伤害计算（穿透加成） ──────────────────────────────────────────

    private static int GetTotalActualDamageToPlayer(object combatManager, object player)
    {
        var total = 0;
        var enemies = GetEnemies(combatManager);
        if (enemies == null) return 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            var (perHit, hitCount) = GetEnemyIntentDamage(enemy);
            if (perHit <= 0) continue;
            var baseDamage = perHit * hitCount;

            // 穿透所有加成
            var actual = CalculateActualDamageToPlayer(baseDamage, player);
            total += actual;
        }

        return total;
    }

    /// <summary>计算实际伤害（穿透所有加成）</summary>
    private static int CalculateActualDamageToPlayer(int baseDamage, object player)
    {
        if (ModifyHpLostMethod != null)
        {
            try
            {
                var result = ModifyHpLostMethod.Invoke(player, [baseDamage]);
                if (result is decimal d) return (int)Math.Ceiling(d);
                if (result is int i) return i;
            }
            catch { }
        }

        // 手动计算：易伤 +50%，虚弱 -25%
        var vuln = GetPowerStack(player, "Vulnerable");
        var weak = GetPowerStack(player, "Weak");
        var mult = 1.0 + vuln * 0.5 - weak * 0.25;
        return (int)Math.Ceiling(baseDamage * Math.Max(0.25, mult));
    }

    /// <summary>从 Powers 列表找指定名称的 power 层数</summary>
    private static int GetPowerStack(object creature, string powerName)
    {
        var type = creature.GetType();
        foreach (var propName in new[] { "Powers", "StatusEffects", "Buffs" })
        {
            if (AccessTools.Property(type, propName)?.GetValue(creature) is not System.Collections.IEnumerable powers)
                continue;
            foreach (var power in powers)
            {
                if (power == null) continue;
                var pt = power.GetType();
                if (!pt.Name.Contains(powerName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var amountProp in new[] { "Amount", "Count", "Stacks" })
                {
                    var ap = AccessTools.Property(pt, amountProp);
                    if (ap?.GetValue(power) is int a) return a;
                }
                return 1;
            }
        }
        return 0;
    }

    // ── 敌人意图读取 ────────────────────────────────────────────────────

    /// <returns>(perHit, hitCount) — 单段伤害和段数</returns>
    private static (int perHit, int hitCount) GetEnemyIntentDamage(object enemy)
    {
        var type = enemy.GetType();

        // 意图对象
        var intentProp = AccessTools.Property(type, "Intent");
        object? intent = intentProp?.GetValue(enemy);

        // 如果 enemy 没有 Intent 属性，尝试直接读 Damage 和 Repeat
        if (intent == null)
        {
            var dmg = GetIntProperty(type, enemy, "Damage", "IntentDamage", "AttackDamage");
            var repeat = GetIntProperty(type, enemy, "Repeat", "HitCount", "Attacks");
            return (dmg, Math.Max(1, repeat));
        }

        var intentType = intent.GetType();
        var damage = GetIntProperty(intentType, intent, "Damage", "Amount", "Value");
        var repeat = GetIntProperty(intentType, intent, "Repeat", "HitCount", "Attacks", "Multiplier");
        return (damage, Math.Max(1, repeat));
    }

    private static int GetIntProperty(Type type, object instance, params string[] names)
    {
        foreach (var name in names)
        {
            var prop = AccessTools.Property(type, name);
            if (prop == null) continue;
            var val = prop.GetValue(instance);
            if (val is int i) return i;
            if (val is decimal d) return (int)d;
        }
        return 0;
    }

    // ── 战斗状态读取 ──────────────────────────────────────────────────────

    private static System.Collections.IEnumerable? GetEnemies(object combatManager)
    {
        var type = combatManager.GetType();
        foreach (var name in new[] { "Enemies", "Monsters", "Creatures", "AllCreatures" })
        {
            var prop = AccessTools.Property(type, name);
            if (prop?.GetValue(combatManager) is System.Collections.IEnumerable e) return e;
        }

        var method = AccessTools.Method(type, "GetEnemies") ?? AccessTools.Method(type, "GetMonsters");
        if (method?.Invoke(combatManager, null) is System.Collections.IEnumerable r) return r;

        // 兜底：找 Creature 类型的集合属性
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)) continue;
            if (p.GetValue(combatManager) is not System.Collections.IEnumerable list) continue;
            var e = list.GetEnumerator();
            if (!e.MoveNext() || e.Current == null) continue;
            if (CreatureType?.IsInstanceOfType(e.Current) == true) return list;
        }
        return null;
    }

    private static object? GetPlayer(object combatManager)
    {
        var type = combatManager.GetType();
        foreach (var name in new[] { "Player", "LocalPlayer", "CurrentPlayer" })
        {
            var prop = AccessTools.Property(type, name);
            if (prop?.GetValue(combatManager) is { } p && CreatureType?.IsInstanceOfType(p) == true)
                return p;
        }
        return null;
    }

    // ── 玩家头顶显示 ──────────────────────────────────────────────────────

    private static void UpdatePlayerLabel(int netDamage, int totalRaw, int block, object player)
    {
        if (netDamage <= 0) { ClearPlayerLabel(); return; }
        if (netDamage == _lastPlayerNetDamage && _playerLabel != null
                                               && GodotObject.IsInstanceIdValid(_playerLabel.GetInstanceId()))
            return;

        _lastPlayerNetDamage = netDamage;
        var node = GetCreatureNode(player);
        if (node == null) node = FindCreatureNodeInScene(player);
        if (node == null) return;

        ClearPlayerLabel();
        _playerLabel = CreateLabel($"-{netDamage}", 52);
        _playerLabel.Modulate = new Color(1.0f, 0.1f, 0.1f);

        node.AddChild(_playerLabel);
        _playerLabel.Owner = node.Owner ?? node;

        var baseY = GetHeadHeight(node) + 0.5f;
        _playerLabel.Position = new Vector3(0, baseY, 0);

        _playerTween = _playerLabel.CreateTween();
        _playerTween.SetLoops();
        _playerTween.SetParallel(true);
        _playerTween.TweenProperty(_playerLabel, "position:y", baseY + 0.2f, 1.0f).SetTrans(Tween.TransitionType.Sine);
        _playerTween.TweenProperty(_playerLabel, "position:y", baseY - 0.05f, 1.0f).SetTrans(Tween.TransitionType.Sine);
    }

    private static void ClearPlayerLabel()
    {
        if (_playerTween != null && GodotObject.IsInstanceIdValid(_playerTween.GetInstanceId()))
            _playerTween.Kill();
        if (_playerLabel != null && GodotObject.IsInstanceIdValid(_playerLabel.GetInstanceId()))
            _playerLabel.QueueFree();
        _playerLabel = null;
        _playerTween = null;
        _lastPlayerNetDamage = int.MinValue;
    }

    // ── 敌人头顶显示 ──────────────────────────────────────────────────────

    private static void UpdateEnemyLabels(object combatManager)
    {
        var enemies = GetEnemies(combatManager);
        if (enemies == null) return;

        var seen = new HashSet<int>();

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            var id = enemy.GetHashCode();
            seen.Add(id);

            var (perHit, hitCount) = GetEnemyIntentDamage(enemy);
            if (perHit <= 0)
            {
                RemoveEnemyLabel(id);
                continue;
            }

            // 格式化文本
            string text;
            var total = perHit * hitCount;
            if (hitCount > 1)
                text = $"{perHit}×{hitCount} ({total})";
            else
                text = total.ToString();

            ShowEnemyLabel(id, enemy, text, perHit, hitCount);
        }

        // 清理不存在的敌人
        foreach (var key in EnemyLabels.Keys)
            if (!seen.Contains(key))
                RemoveEnemyLabel(key);
    }

    private static void ShowEnemyLabel(int enemyId, object enemy, string text, int perHit, int hitCount)
    {
        // 已存在且文本相同 → 跳过
        if (EnemyLabels.TryGetValue(enemyId, out var existing) && existing != null
                                                               && GodotObject.IsInstanceIdValid(existing.GetInstanceId())
                                                               && existing.Text == text)
            return;

        // 清理旧的
        RemoveEnemyLabel(enemyId);

        var node = GetCreatureNode(enemy);
        if (node == null) node = FindCreatureNodeInScene(enemy);
        if (node == null) return;

        var label = CreateLabel(text, 44);
        label.Modulate = new Color(1.0f, 0.1f, 0.1f);

        node.AddChild(label);
        label.Owner = node.Owner ?? node;

        var baseY = GetHeadHeight(node) + 0.3f;
        label.Position = new Vector3(0, baseY, 0);

        EnemyLabels[enemyId] = label;
    }

    private static void RemoveEnemyLabel(int enemyId)
    {
        if (!EnemyLabels.TryRemove(enemyId, out var label)) return;
        if (label != null && GodotObject.IsInstanceIdValid(label.GetInstanceId()))
            label.QueueFree();
    }

    // ── 工具方法 ──────────────────────────────────────────────────────────

    private static Label3D CreateLabel(string text, int fontSize)
    {
        return new Label3D
        {
            Text = text,
            FontSize = fontSize,
            OutlineSize = 6,
            OutlineModulate = Colors.Black,
            PixelSize = 0.01f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
    }

    private static float GetHeadHeight(Node3D node)
    {
        var aabb = node.GetAabb();
        return aabb.Size.Y > 0.01f ? aabb.Size.Y : 2.0f;
    }

    private static Node3D? GetCreatureNode(object creature)
    {
        var type = creature.GetType();
        var prop = AccessTools.Property(type, "Node")
                   ?? AccessTools.Property(type, "CreatureNode")
                   ?? AccessTools.Property(type, "ViewNode")
                   ?? AccessTools.Property(type, "ModelNode");
        if (prop?.GetValue(creature) is Node3D n) return n;
        return creature as Node3D;
    }

    private static Node3D? FindCreatureNodeInScene(object creature)
    {
        try
        {
            var idProp = AccessTools.Property(creature.GetType(), "CreatureId")
                         ?? AccessTools.Property(creature.GetType(), "Id")
                         ?? AccessTools.Property(creature.GetType(), "Name");
            var id = idProp?.GetValue(creature)?.ToString();
            if (id == null || id == "Player" || id == "0") return null;

            if (Engine.GetMainLoop() is not SceneTree tree) return null;
            return FindNode3D(tree.Root, id);
        }
        catch { return null; }
    }

    private static Node3D? FindNode3D(Node node, string name)
    {
        if (node is Node3D n3d && n3d.Name.ToString().Contains(name, StringComparison.OrdinalIgnoreCase))
            return n3d;
        foreach (var child in node.GetChildren())
        {
            var r = FindNode3D(child, name);
            if (r != null) return r;
        }
        return null;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Hook 点
// ═══════════════════════════════════════════════════════════════════════════════

[HarmonyPatch]
public static class DamagePreviewTurnStartPatch
{
    private static readonly MethodInfo? Target = FindTarget();

    private static MethodInfo? FindTarget()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (t == null) return null;
        foreach (var n in new[] { "StartTurn", "OnTurnStart", "BeginTurn", "StartPlayerTurn", "StartEnemyTurn" })
        {
            var m = AccessTools.DeclaredMethod(t, n);
            if (m != null) return m;
        }
        return null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodInfo? TargetMethod() => Target;

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target != null) { Log.Info($"[伤害预测] 回合开始 Hook: CombatManager.{Target.Name}"); return true; }
        Log.Error("[伤害预测] 未找到回合开始方法");
        return false;
    }

    private static void Postfix() => DamagePreviewPatch.RefreshAll();
}

[HarmonyPatch]
public static class DamagePreviewCardPlayedPatch
{
    private static readonly MethodInfo? Target = FindTarget();

    private static MethodInfo? FindTarget()
    {
        var t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (t == null) return null;
        foreach (var n in new[] { "PlayCard", "OnCardPlayed", "ExecuteCard", "ResolveCard" })
        {
            var m = AccessTools.DeclaredMethod(t, n);
            if (m != null) return m;
        }
        return null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodInfo? TargetMethod() => Target;

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target != null) { Log.Info($"[伤害预测] 卡牌打出 Hook: CombatManager.{Target.Name}"); return true; }
        Log.Error("[伤害预测] 未找到卡牌打出方法");
        return false;
    }

    private static void Postfix() => DamagePreviewPatch.RefreshAll();
}