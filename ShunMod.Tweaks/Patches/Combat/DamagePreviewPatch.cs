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
// 场景：战斗中，玩家头顶实时显示「本回合所有敌人攻击意图的实际伤害总和 − 玩家格挡」
//       实际伤害 = 意图伤害 × 易伤等加成，与游戏内实际扣血一致
//
// 原理：Hook CombatManager 的回合开始/结束流程，对每个敌人：
//   1. 读取意图基础伤害
//   2. 如果是玩家受到伤害，尝试调用 Creature.ModifyHpLost 穿透所有加成
//   3. 否则用已知 modifier 手动计算（易伤 +50% 等）
// 然后减去玩家当前格挡，在头顶显示净伤害。
//
// 更新时机：回合开始、卡牌打出后刷新。
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

    /// <summary>Creature.ModifyHpLost(decimal) — 穿透所有伤害加成</summary>
    private static readonly MethodInfo? ModifyHpLostMethod = FindModifyHpLost();

    /// <summary>Creature 上表示易伤状态的属性/方法缓存</summary>
    private static PropertyInfo? _vulnerableProp;

    // 玩家头顶的 label
    private static Label3D? _playerLabel;
    private static Tween? _playerTween;
    private static int _lastDisplayedNetDamage = int.MinValue;

    // ── 反射初始化 ────────────────────────────────────────────────────────

    private static MethodInfo? FindModifyHpLost()
    {
        if (CreatureType == null) return null;
        var method = AccessTools.DeclaredMethod(CreatureType, "ModifyHpLost", [typeof(decimal)]);
        if (method != null) return method;

        // 尝试其他常见名称
        foreach (var name in new[] { "ModifyHpLost", "CalculateDamageTaken", "ApplyDamageModifiers", "GetFinalDamage" })
        {
            var m = AccessTools.DeclaredMethod(CreatureType, name, [typeof(decimal)]);
            if (m != null) return m;
        }

        // 不限制参数个数
        foreach (var name in new[] { "ModifyHpLost", "CalculateDamageTaken", "GetFinalDamage" })
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

            // 计算玩家将受到的净伤害（已穿透所有加成）
            var totalActualDamage = GetTotalActualDamageToPlayer(combat);
            var playerBlock = GetPlayerBlock(combat);
            var netDamage = Math.Max(0, totalActualDamage - playerBlock);

            UpdatePlayerLabel(netDamage, totalActualDamage, playerBlock);
        }
        catch (Exception ex)
        {
            Log.Error($"[伤害预测] RefreshAll 异常: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── 伤害计算（穿透加成） ──────────────────────────────────────────────

    /// <summary>计算所有敌人对玩家的实际伤害总和（已穿透易伤等加成）</summary>
    private static int GetTotalActualDamageToPlayer(object combatManager)
    {
        var total = 0;
        var enemies = GetEnemies(combatManager);
        if (enemies == null) return 0;

        var player = GetPlayer(combatManager);
        if (player == null) return 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            var baseDamage = GetEnemyIntentDamage(enemy);
            if (baseDamage <= 0) continue;

            // 穿透所有加成，计算实际伤害
            var actualDamage = CalculateActualDamageToPlayer(enemy, baseDamage, player);
            total += actualDamage;
        }

        return total;
    }

    /// <summary>计算敌人对玩家的实际伤害（穿透所有加成）</summary>
    private static int CalculateActualDamageToPlayer(object enemy, int baseDamage, object player)
    {
        // 方案 1: 调用 Creature.ModifyHpLost(decimal) 穿透所有加成
        if (ModifyHpLostMethod != null)
        {
            try
            {
                var result = ModifyHpLostMethod.Invoke(player, [baseDamage]);
                if (result is decimal d)
                    return (int)Math.Ceiling(d);
                if (result is int i)
                    return i;
            }
            catch
            {
                // 如果方法有副作用或参数不对，fallback
            }
        }

        // 方案 2: 手动计算已知 modifier
        // 先查易伤（Vulnerable）—— 通常 +50% 伤害
        var vulnerableMultiplier = 1.0;
        var vulnLevel = GetVulnerableLevel(player);
        if (vulnLevel > 0)
            vulnerableMultiplier = 1.0 + vulnLevel * 0.5;

        // 查敌人力量加成
        var enemyStrength = GetCreatureStrength(enemy);
        var strengthBonus = Math.Max(0, enemyStrength);

        // 查玩家虚弱（Weak）—— 通常 -25% 伤害
        var weakMultiplier = 1.0;
        var weakLevel = GetWeakLevel(player);
        if (weakLevel > 0)
            weakMultiplier = 1.0 - weakLevel * 0.25;

        var actual = (int)Math.Ceiling(baseDamage * vulnerableMultiplier * weakMultiplier + strengthBonus);
        return Math.Max(0, actual);
    }

    /// <summary>获取 Creature 的易伤层数</summary>
    private static int GetVulnerableLevel(object creature)
    {
        var type = creature.GetType();

        // 尝试 Vulnerable 属性
        _vulnerableProp ??= AccessTools.Property(type, "Vulnerable")
                             ?? AccessTools.Property(type, "VulnerableAmount")
                             ?? AccessTools.Property(type, "VulnerableCount");
        if (_vulnerableProp?.GetValue(creature) is int v)
            return v;

        // 尝试获取 Power 列表，找 VulnerablePower
        var powersProp = AccessTools.Property(type, "Powers")
                         ?? AccessTools.Property(type, "StatusEffects")
                         ?? AccessTools.Property(type, "Buffs");
        if (powersProp?.GetValue(creature) is System.Collections.IEnumerable powers)
        {
            foreach (var power in powers)
            {
                if (power == null) continue;
                var powerType = power.GetType();
                if (powerType.Name.Contains("Vulnerable", StringComparison.OrdinalIgnoreCase))
                {
                    var amountProp = AccessTools.Property(powerType, "Amount")
                                     ?? AccessTools.Property(powerType, "Count")
                                     ?? AccessTools.Property(powerType, "Stacks");
                    if (amountProp?.GetValue(power) is int a)
                        return a;
                    return 1; // 存在即算 1 层
                }
            }
        }

        return 0;
    }

    /// <summary>获取 Creature 的虚弱层数</summary>
    private static int GetWeakLevel(object creature)
    {
        var type = creature.GetType();

        var weakProp = AccessTools.Property(type, "Weak")
                       ?? AccessTools.Property(type, "WeakAmount")
                       ?? AccessTools.Property(type, "WeakCount");
        if (weakProp?.GetValue(creature) is int w)
            return w;

        // 遍历 Powers 找 WeakPower
        var powersProp = AccessTools.Property(type, "Powers")
                         ?? AccessTools.Property(type, "StatusEffects")
                         ?? AccessTools.Property(type, "Buffs");
        if (powersProp?.GetValue(creature) is System.Collections.IEnumerable powers)
        {
            foreach (var power in powers)
            {
                if (power == null) continue;
                var powerType = power.GetType();
                if (powerType.Name.Contains("Weak", StringComparison.OrdinalIgnoreCase))
                {
                    var amountProp = AccessTools.Property(powerType, "Amount")
                                     ?? AccessTools.Property(powerType, "Count");
                    if (amountProp?.GetValue(power) is int a)
                        return a;
                    return 1;
                }
            }
        }

        return 0;
    }

    /// <summary>获取 Creature 的力量值</summary>
    private static int GetCreatureStrength(object creature)
    {
        var type = creature.GetType();

        var strengthProp = AccessTools.Property(type, "Strength")
                           ?? AccessTools.Property(type, "TempStrength");
        if (strengthProp?.GetValue(creature) is int s)
            return s;

        // 遍历 Powers 找 StrengthPower
        var powersProp = AccessTools.Property(type, "Powers")
                         ?? AccessTools.Property(type, "StatusEffects")
                         ?? AccessTools.Property(type, "Buffs");
        if (powersProp?.GetValue(creature) is System.Collections.IEnumerable powers)
        {
            foreach (var power in powers)
            {
                if (power == null) continue;
                var powerType = power.GetType();
                if (powerType.Name.Contains("Strength", StringComparison.OrdinalIgnoreCase))
                {
                    var amountProp = AccessTools.Property(powerType, "Amount")
                                     ?? AccessTools.Property(powerType, "Count");
                    if (amountProp?.GetValue(power) is int a)
                        return a;
                }
            }
        }

        return 0;
    }

    // ── 战斗状态读取 ──────────────────────────────────────────────────────

    private static System.Collections.IEnumerable? GetEnemies(object combatManager)
    {
        var type = combatManager.GetType();

        var prop = AccessTools.Property(type, "Enemies")
                   ?? AccessTools.Property(type, "Monsters")
                   ?? AccessTools.Property(type, "Creatures")
                   ?? AccessTools.Property(type, "AllCreatures");
        if (prop?.GetValue(combatManager) is System.Collections.IEnumerable enumerable)
            return enumerable;

        var method = AccessTools.Method(type, "GetEnemies")
                     ?? AccessTools.Method(type, "GetMonsters")
                     ?? AccessTools.Method(type, "GetCreatures");
        if (method?.Invoke(combatManager, null) is System.Collections.IEnumerable result)
            return result;

        // 兜底：找所有 Creature 类型的集合属性
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)) continue;
            var val = p.GetValue(combatManager);
            if (val is System.Collections.IEnumerable list)
            {
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

    private static int GetEnemyIntentDamage(object enemy)
    {
        var type = enemy.GetType();

        // 尝试 Intent.Damage
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

        // 直接属性
        foreach (var name in new[] { "IntentDamage", "AttackDamage", "PlannedDamage", "Damage" })
        {
            var prop = AccessTools.Property(type, name);
            if (prop?.GetValue(enemy) is int d) return d;
        }

        // 方法
        foreach (var name in new[] { "GetIntentDamage", "GetAttackDamage", "GetPlannedDamage" })
        {
            var method = AccessTools.Method(type, name);
            if (method?.Invoke(enemy, null) is int d) return d;
        }

        // GetIntent() → .Damage
        var getIntent = AccessTools.Method(type, "GetIntent");
        if (getIntent?.Invoke(enemy, null) is { } intentObj)
        {
            var dProp = AccessTools.Property(intentObj.GetType(), "Damage")
                        ?? AccessTools.Property(intentObj.GetType(), "Amount");
            if (dProp?.GetValue(intentObj) is int d) return d;
        }

        return 0;
    }

    private static int GetPlayerBlock(object combatManager)
    {
        var player = GetPlayer(combatManager);
        return player != null ? CreatureReflection.GetBlock(player) : 0;
    }

    private static object? GetPlayer(object combatManager)
    {
        var type = combatManager.GetType();

        foreach (var name in new[] { "Player", "LocalPlayer", "CurrentPlayer" })
        {
            var prop = AccessTools.Property(type, name);
            if (prop?.GetValue(combatManager) is { } player && CreatureType?.IsInstanceOfType(player) == true)
                return player;
        }

        return null;
    }

    // ── 头顶显示 ──────────────────────────────────────────────────────────

    private static void UpdatePlayerLabel(int netDamage, int totalRawDamage, int playerBlock)
    {
        if (netDamage <= 0 && totalRawDamage <= 0)
        {
            ClearPlayerLabel();
            return;
        }

        if (netDamage == _lastDisplayedNetDamage && _playerLabel != null
                                                 && GodotObject.IsInstanceIdValid(_playerLabel.GetInstanceId()))
            return;

        _lastDisplayedNetDamage = netDamage;

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

        ClearPlayerLabel();

        _playerLabel = new Label3D();
        _playerLabel.Text = $"-{netDamage}";
        _playerLabel.FontSize = 52;
        _playerLabel.OutlineSize = 6;
        _playerLabel.OutlineModulate = Colors.Black;
        _playerLabel.PixelSize = 0.01f;
        _playerLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _playerLabel.NoDepthTest = true;

        // 颜色
        if (netDamage > 0 && playerBlock > 0)
            _playerLabel.Modulate = new Color(1.0f, 0.6f, 0.0f); // 橙色：挡了一部分
        else if (netDamage > 0)
            _playerLabel.Modulate = new Color(1.0f, 0.1f, 0.1f); // 红色：纯伤害
        else
            _playerLabel.Modulate = new Color(0.2f, 1.0f, 0.2f); // 绿色：全挡住

        node.AddChild(_playerLabel);
        _playerLabel.Owner = node.Owner ?? node;

        var aabb = node.GetAabb();
        var baseY = aabb.Size.Y > 0.01f ? aabb.Size.Y + 0.5f : 2.2f;
        _playerLabel.Position = new Vector3(0, baseY, 0);

        // 脉动
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

        if (prop?.GetValue(creature) is Node3D node) return node;
        if (creature is Node3D self) return self;
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
            if (creatureId == null || creatureId == "Player" || creatureId == "0") return null;

            var tree = Engine.GetMainLoop();
            if (tree is not SceneTree sceneTree) return null;

            return FindFirstNode3DRecursive(sceneTree.Root, creatureId);
        }
        catch { return null; }
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
// 回合开始 Hook — 触发伤害预测刷新
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用

/// <summary>Hook 回合开始，刷新伤害预测</summary>
[HarmonyPatch]
public static class DamagePreviewTurnStartPatch
{
    private static readonly MethodInfo? Target = FindTarget();

    private static MethodInfo? FindTarget()
    {
        var combatType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager");
        if (combatType == null) return null;
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
        if (Target != null) { Log.Info($"[伤害预测] 回合开始 Hook: CombatManager.{Target.Name}"); return true; }
        Log.Error("[伤害预测] 未找到回合开始方法，跳过");
        return false;
    }

    private static void Postfix() => DamagePreviewPatch.RefreshAll();
}

/// <summary>Hook 卡牌打出，刷新伤害预测</summary>
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
        if (Target != null) { Log.Info($"[伤害预测] 卡牌打出 Hook: CombatManager.{Target.Name}"); return true; }
        Log.Error("[伤害预测] 未找到卡牌打出方法，跳过");
        return false;
    }

    private static void Postfix() => DamagePreviewPatch.RefreshAll();
}