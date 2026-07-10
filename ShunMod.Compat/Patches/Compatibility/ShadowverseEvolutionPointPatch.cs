using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 进化不消耗进化点。
/// 初始给 1 点启动，进化时不消耗点数，每回合可多次进化。
///
/// 方案：
///   Initialize → Prefix 拦截 ref 参数，设为基础值 (1 点)
///   TryUseEvolutionPoint → Prefix 跳过原方法（阻止进化点递减）
///   CanEvolve / HasEvolvedThisTurn → Postfix 解除回合限制
///
/// 纯反射，不引用 shadowverse.dll。
/// </summary>
public static class ShadowverseEvolutionPointPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "EvolutionPointManager";

    private static Type? _evoMgrType;
    private static FieldInfo? _pointsField;
    private static object? _pointsChangedEvent; // EventInfo
    private static List<FieldInfo> _turnFlagFields = []; // 回合内进化标记字段

    public static void Apply(Harmony harmony)
    {
        _evoMgrType = CompatibilityPatchUtil.FindPatchType(ModId, TargetNs, TargetType);
        if (_evoMgrType == null) return;

        // 发现 _points 字段（尝试 static，再尝试 instance）
        DiscoverFields();

        // ── Patch 1: Initialize(player, evolvePoints=2, superEvolvePoints=2) ──
        // 默认参数由编译器在调用点内联，Prefix 用 ref int 截获
        var initMethod = AccessTools.Method(_evoMgrType, "Initialize");
        if (initMethod != null)
        {
            harmony.Patch(initMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch), nameof(Initialize_Prefix)),
                postfix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch), nameof(Initialize_Postfix)));
            Log.Info($"[{ModId}] Shadow verse EvolutionPoint: Initialize patched (Prefix+Postfix, 2→1, no consumption)");
        }
        else
        {
            Log.Info($"[{ModId}] Shadow verse EvolutionPoint: Initialize method not found");
        }

        // ── 不追加 AddEvolvePoints/AddSuperEvolvePoints Transpiler ──
        // 进化不消耗点数，无需修改 Math.Min 上限，保留游戏原始数值逻辑。

        // ── Patch 2/3: 解除一回合一次进化限制 ──
        PatchTurnLimit(harmony);
    }

    // ═══════════════════════════════════════════════
    //  回合限制解除
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 解除一回合一次进化限制。
    /// 策略：
    ///   1. 扫描 EvolutionPointManager 的所有非公开 bool 字段，记录可能是回合标记的字段
    ///   2. Patch CanEvolve / HasEvolvedThisTurn / TryUseEvolutionPoint 等限制方法
    ///   3. AddEvolvePoints 后 Postfix 重置所有 bool 字段（兜底）
    /// </summary>
    private static void PatchTurnLimit(Harmony harmony)
    {
        if (_evoMgrType == null) return;

        // ── Step 1: 扫描所有 bool 字段 ──
        var allFields = _evoMgrType.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        foreach (var f in allFields)
        {
            if (f.FieldType == typeof(bool))
            {
                _turnFlagFields.Add(f);
                Log.Info($"[{ModId}] EvolutionPoint: found bool field '{f.Name}' (static={f.IsStatic})");
            }
        }

        // ── Step 2: 尝试 patch CanEvolve / HasEvolvedThisTurn 等方法 ──
        var canEvolveNames = new[] { "CanEvolve", "CanUseEvolutionPoint", "CanPlayerEvolve",
            "IsEvolveAvailable", "HasEvolutionAvailable" };
        foreach (var name in canEvolveNames)
        {
            var method = AccessTools.Method(_evoMgrType, name);
            if (method != null && method.ReturnType == typeof(bool))
            {
                harmony.Patch(method,
                    postfix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch),
                        nameof(CanEvolve_Postfix)));
                Log.Info($"[{ModId}] EvolutionPoint: {name} → always return true");
                break; // 只 patch 第一个找到的
            }
        }

        // HasEvolvedThisTurn → always return false
        var hasEvolvedNames = new[] { "HasEvolvedThisTurn", "HasEvolved",
            "HasUsedEvolutionThisTurn", "IsEvolvedThisTurn" };
        foreach (var name in hasEvolvedNames)
        {
            var method = AccessTools.Method(_evoMgrType, name);
            if (method != null && method.ReturnType == typeof(bool))
            {
                harmony.Patch(method,
                    postfix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch),
                        nameof(HasEvolved_Postfix)));
                Log.Info($"[{ModId}] EvolutionPoint: {name} → always return false");
                break;
            }
        }

        // TryUseEvolutionPoint → always succeed (skip check)
        var tryUseNames = new[] { "TryUseEvolutionPoint", "TryUseEvolvePoint",
            "ConsumeEvolutionPoint", "UseEvolutionPoint" };
        foreach (var name in tryUseNames)
        {
            var method = AccessTools.Method(_evoMgrType, name);
            if (method != null)
            {
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch),
                        nameof(TryUse_Prefix)));
                Log.Info($"[{ModId}] EvolutionPoint: {name} → skip original (no consumption)");
                break;
            }
        }

        // ── Step 3: AddEvolvePoints Postfix — 重置所有 bool 标记 ──
        var addEvo = AccessTools.Method(_evoMgrType, "AddEvolvePoints");
        if (addEvo != null)
        {
            harmony.Patch(addEvo,
                postfix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch),
                    nameof(ResetTurnFlags_Postfix)));
            Log.Info($"[{ModId}] EvolutionPoint: AddEvolvePoints Postfix → reset turn flags");
        }

        var addSuper = AccessTools.Method(_evoMgrType, "AddSuperEvolvePoints");
        if (addSuper != null)
        {
            harmony.Patch(addSuper,
                postfix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch),
                    nameof(ResetTurnFlags_Postfix)));
            Log.Info($"[{ModId}] EvolutionPoint: AddSuperEvolvePoints Postfix → reset turn flags");
        }
    }

    /// <summary>CanEvolve → 永远返回 true</summary>
    private static void CanEvolve_Postfix(ref bool __result)
    {
        __result = true;
    }

    /// <summary>HasEvolvedThisTurn → 永远返回 false</summary>
    private static void HasEvolved_Postfix(ref bool __result)
    {
        __result = false;
    }

    /// <summary>TryUseEvolutionPoint → 跳过原方法，进化始终成功且不消耗点数</summary>
    /// <remarks>
    /// Harmony Prefix 返回 false 时跳过原方法体，__result 设定为 true（调用者得到进化成功信号）。
    /// 原方法中消耗点数的逻辑不会执行，实现"不消耗"。
    /// </remarks>
    private static bool TryUse_Prefix(ref bool __result)
    {
        ResetAllBoolFlags();
        __result = true;   // 进化始终成功
        return false;      // 跳过原方法 → 进化点不递减
    }

    /// <summary>每次 AddEvolvePoints 后重置所有 bool 标记字段</summary>
    private static void ResetTurnFlags_Postfix()
    {
        ResetAllBoolFlags();
    }

    /// <summary>将 EvolutionPointManager 的所有非公开 bool 字段设为 false</summary>
    private static void ResetAllBoolFlags()
    {
        foreach (var f in _turnFlagFields)
        {
            try
            {
                var target = f.IsStatic ? null : CompatibilityPatchUtil.FindManagerInstance(_evoMgrType!);
                if (target != null || f.IsStatic)
                {
                    f.SetValue(target, false);
                }
            }
            catch
            {
                // 静默跳过
            }
        }
    }

    /// <summary>
    /// 发现 _points 字段和 PointsChanged 事件。
    /// 兼容不同版本的 shadowverse mod（字段可能是 static 或 instance）。
    /// </summary>
    private static void DiscoverFields()
    {
        if (_evoMgrType == null) return;

        // _points: 先试 static private，再试 instance private
        _pointsField = _evoMgrType.GetField("_points",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? _evoMgrType.GetField("_points",
                BindingFlags.NonPublic | BindingFlags.Instance);

        if (_pointsField != null)
        {
            Log.Info($"[{ModId}] EvolutionPoint: _points field found (static={_pointsField.IsStatic})");
        }
        else
        {
            Log.Info($"[{ModId}] EvolutionPoint: _points field NOT found — Postfix fallback will be skipped");
        }

        // PointsChanged: 可能是 event 或 field
        var evt = _evoMgrType.GetEvent("PointsChanged",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (evt != null)
        {
            _pointsChangedEvent = evt;
            Log.Info($"[{ModId}] EvolutionPoint: PointsChanged event found");
        }
        else
        {
            var fld = _evoMgrType.GetField("PointsChanged",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (fld != null)
            {
                _pointsChangedEvent = fld;
                Log.Info($"[{ModId}] EvolutionPoint: PointsChanged field found");
            }
            else
            {
                Log.Info($"[{ModId}] EvolutionPoint: PointsChanged NOT found — UI may not refresh");
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  Initialize — Prefix + Postfix
    // ═══════════════════════════════════════════════

    /// <summary>Prefix: 把默认参数改为 1（编译器在调用点内联，ref 截获）</summary>
    /// <remarks>进化不消耗点数，1 点启动即可无限进化。</remarks>
    private static void Initialize_Prefix(ref int evolvePoints, ref int superEvolvePoints)
    {
        evolvePoints = 1;
        superEvolvePoints = 1;
    }

    /// <summary>Postfix: 兜底 — 如果 Prefix 没匹配到参数，直接改 _points 字典</summary>
    private static void Initialize_Postfix(Player __0)
    {
        if (_pointsField == null || __0 == null) return;

        try
        {
            var target = _pointsField.IsStatic ? null : CompatibilityPatchUtil.FindManagerInstance(_evoMgrType!);
            if (target == null && !_pointsField.IsStatic) return;

            var points = _pointsField.GetValue(target);
            if (points is IDictionary<Player, (int, int)> dict)
            {
                if (dict.TryGetValue(__0, out var current))
                {
                    dict[__0] = (1, 1);
                    FirePointsChanged();
                    Log.Info($"[{ModId}] EvolutionPoint: Postfix set 1 point for player (was {current})");
                }
            }
            else if (points is IDictionary<Player, ValueTuple<int, int>> dict2)
            {
                if (dict2.TryGetValue(__0, out _))
                {
                    dict2[__0] = (1, 1);
                    FirePointsChanged();
                    Log.Info($"[{ModId}] EvolutionPoint: Postfix set 1 point for player (ValueTuple)");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Info($"[{ModId}] EvolutionPoint: Postfix failed — {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    private static void FirePointsChanged()
    {
        try
        {
            if (_pointsChangedEvent is EventInfo evt)
            {
                var backingField = _evoMgrType?.GetField(evt.Name,
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (backingField != null)
                {
                    var target = backingField.IsStatic ? null : CompatibilityPatchUtil.FindManagerInstance(_evoMgrType!);
                    var del = backingField.GetValue(target) as Delegate;
                    del?.DynamicInvoke();
                }
            }
            else if (_pointsChangedEvent is FieldInfo fld)
            {
                var target = fld.IsStatic ? null : CompatibilityPatchUtil.FindManagerInstance(_evoMgrType!);
                var del = fld.GetValue(target) as Delegate;
                del?.DynamicInvoke();
            }
        }
        catch
        {
            // 静默失败 — PointsChanged 不是关键路径
        }
    }

}
