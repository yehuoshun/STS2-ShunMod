using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除进化点限制。
/// 每场战斗初始 99 进化点，取消 AddEvolvePoints / AddSuperEvolvePoints 的 Math.Min(..., 2) 硬上限。
///
/// 方案：
///   Initialize → Prefix 拦截 ref 参数（默认值由编译器在调用点内联，只能 Prefix 截获）
///   AddEvolvePoints / AddSuperEvolvePoints → Transpiler 替换 IL 常量 2 → 99
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

    public static void Apply(Harmony harmony)
    {
        _evoMgrType = FindType();
        if (_evoMgrType == null)
        {
            Log.Info($"[{ModId}] Shadowverse mod not detected, skipping EvolutionPoint patch");
            return;
        }

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
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: Initialize patched (Prefix+Postfix, 2→99)");
        }
        else
        {
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: Initialize method not found");
        }

        // ── Patch 2: AddEvolvePoints Transpiler — Math.Min(..., 2) → Math.Min(..., 99) ──
        var addEvoMethod = AccessTools.Method(_evoMgrType, "AddEvolvePoints");
        if (addEvoMethod != null)
        {
            harmony.Patch(addEvoMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch),
                    nameof(Replace2With99_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: AddEvolvePoints cap removed (2→99)");
        }
        else
        {
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: AddEvolvePoints method not found");
        }

        // ── Patch 3: AddSuperEvolvePoints Transpiler — 同上 ──
        var addSuperMethod = AccessTools.Method(_evoMgrType, "AddSuperEvolvePoints");
        if (addSuperMethod != null)
        {
            harmony.Patch(addSuperMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch),
                    nameof(Replace2With99_Transpiler)));
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: AddSuperEvolvePoints cap removed (2→99)");
        }
        else
        {
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: AddSuperEvolvePoints method not found");
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

    /// <summary>Prefix: 把默认参数 2 改成 99（编译器在调用点内联，ref 截获）</summary>
    private static void Initialize_Prefix(ref int evolvePoints, ref int superEvolvePoints)
    {
        evolvePoints = 99;
        superEvolvePoints = 99;
    }

    /// <summary>Postfix: 兜底 — 如果 Prefix 没匹配到参数，直接改 _points 字典</summary>
    private static void Initialize_Postfix(Player __0)
    {
        if (_pointsField == null || __0 == null) return;

        try
        {
            var target = _pointsField.IsStatic ? null : FindManagerInstance();
            if (target == null && !_pointsField.IsStatic) return;

            var points = _pointsField.GetValue(target);
            if (points is IDictionary<Player, (int, int)> dict)
            {
                if (dict.TryGetValue(__0, out var current))
                {
                    dict[__0] = (99, 99);
                    FirePointsChanged();
                    Log.Info($"[{ModId}] EvolutionPoint: Postfix set 99 points for player (was {current})");
                }
            }
            else if (points is IDictionary<Player, ValueTuple<int, int>> dict2)
            {
                if (dict2.TryGetValue(__0, out var current))
                {
                    dict2[__0] = (99, 99);
                    FirePointsChanged();
                    Log.Info($"[{ModId}] EvolutionPoint: Postfix set 99 points for player (ValueTuple)");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Info($"[{ModId}] EvolutionPoint: Postfix failed — {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════
    //  Transpiler: 替换 IL 常量 2 → 99
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Transpiler: 将 IL 中的 int 常量 2 替换为 99。
    /// 同时匹配 ldc.i4.s 2（短格式）和 ldc.i4 2（长格式）。
    /// </summary>
    private static IEnumerable<CodeInstruction> Replace2With99_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (IsConstant2(inst))
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = 99;
            }
            yield return inst;
        }
    }

    private static bool IsConstant2(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte sb && sb == 2)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int i && i == 2);
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
                    var target = backingField.IsStatic ? null : FindManagerInstance();
                    var del = backingField.GetValue(target) as Delegate;
                    del?.DynamicInvoke();
                }
            }
            else if (_pointsChangedEvent is FieldInfo fld)
            {
                var target = fld.IsStatic ? null : FindManagerInstance();
                var del = fld.GetValue(target) as Delegate;
                del?.DynamicInvoke();
            }
        }
        catch
        {
            // 静默失败 — PointsChanged 不是关键路径
        }
    }

    /// <summary>
    /// 如果 _points 是 instance 字段，尝试获取 EvolutionPointManager 的单例。
    /// 常见模式：static Instance 属性或私有静态 _instance 字段。
    /// </summary>
    private static object? FindManagerInstance()
    {
        if (_evoMgrType == null) return null;

        // 尝试 Instance 属性
        var prop = _evoMgrType.GetProperty("Instance",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (prop != null)
            return prop.GetValue(null);

        // 尝试 _instance 字段
        var fld = _evoMgrType.GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? _evoMgrType.GetField("instance",
                BindingFlags.NonPublic | BindingFlags.Static);
        if (fld != null)
            return fld.GetValue(null);

        return null;
    }

    private static Type? FindType()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType($"{TargetNs}.{TargetType}");
            if (t != null) return t;
        }
        return null;
    }
}
