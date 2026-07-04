using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除进化点限制。
/// 每场战斗初始 99 进化点，取消 AddEvolvePoints / AddSuperEvolvePoints 的硬上限 Math.Min(..., 2)。
/// 纯反射，不引用 shadowverse.dll。
/// </summary>
public static class ShadowverseEvolutionPointPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "EvolutionPointManager";

    private static Type? _evoMgrType;
    private static FieldInfo? _pointsField;
    private static FieldInfo? _pointsChangedField;

    public static void Apply(Harmony harmony)
    {
        _evoMgrType = FindType();
        if (_evoMgrType == null)
        {
            Log.Info($"[{ModId}] Shadowverse mod not detected, skipping EvolutionPoint patch");
            return;
        }

        _pointsField = _evoMgrType.GetField("_points", BindingFlags.NonPublic | BindingFlags.Static);
        _pointsChangedField = _evoMgrType.GetField("PointsChanged", BindingFlags.NonPublic | BindingFlags.Static);

        // 1. Initialize(player, evolvePoints=2, superEvolvePoints=2) → 改成 99
        var initMethod = AccessTools.Method(_evoMgrType, "Initialize");
        if (initMethod != null)
        {
            harmony.Patch(initMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch), nameof(Initialize_Prefix)));
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: Initialize patched (99 points)");
        }

        // 2. AddEvolvePoints → 去掉 Math.Min(..., 2)
        var addEvoMethod = AccessTools.Method(_evoMgrType, "AddEvolvePoints");
        if (addEvoMethod != null)
        {
            harmony.Patch(addEvoMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch), nameof(AddEvolvePoints_Prefix)));
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: AddEvolvePoints cap removed");
        }

        // 3. AddSuperEvolvePoints → 同上
        var addSuperMethod = AccessTools.Method(_evoMgrType, "AddSuperEvolvePoints");
        if (addSuperMethod != null)
        {
            harmony.Patch(addSuperMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseEvolutionPointPatch), nameof(AddSuperEvolvePoints_Prefix)));
            Log.Info($"[{ModId}] Shadowverse EvolutionPoint: AddSuperEvolvePoints cap removed");
        }
    }

    /// <summary>把默认参数 2 改成 99</summary>
    private static void Initialize_Prefix(ref int evolvePoints, ref int superEvolvePoints)
    {
        evolvePoints = 99;
        superEvolvePoints = 99;
    }

    /// <summary>跳过 Math.Min(..., 2)，直接无限加</summary>
    private static bool AddEvolvePoints_Prefix(Player player, int amount)
    {
        var points = (Dictionary<Player, (int, int)>)_pointsField!.GetValue(null)!;
        if (!points.TryGetValue(player, out var current)) return false;

        points[player] = (current.Item1 + amount, current.Item2);
        FirePointsChanged();
        return false; // 跳过原方法
    }

    /// <summary>跳过 Math.Min(..., 2)，直接无限加</summary>
    private static bool AddSuperEvolvePoints_Prefix(Player player, int amount)
    {
        var points = (Dictionary<Player, (int, int)>)_pointsField!.GetValue(null)!;
        if (!points.TryGetValue(player, out var current)) return false;

        points[player] = (current.Item1, current.Item2 + amount);
        FirePointsChanged();
        return false; // 跳过原方法
    }

    private static void FirePointsChanged()
    {
        ((System.Action?)_pointsChangedField!.GetValue(null))?.Invoke();
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