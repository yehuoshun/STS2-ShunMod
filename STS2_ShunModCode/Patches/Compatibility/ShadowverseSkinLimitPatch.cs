using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除皮肤启用数量限制（14→无限）。
/// Patch SkinPackManager.SetEnabled，跳过 GetEnabledCount() >= 14 检查。
/// 纯反射，不引用 shadowverse.dll。
/// </summary>
public static class ShadowverseSkinLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "SkinPackManager";

    private static Type? _skinMgrType;
    private static FieldInfo? _prefsField;

    public static void Apply(Harmony harmony)
    {
        _skinMgrType = FindType();
        if (_skinMgrType == null)
        {
            Log.Info($"[{ModId}] Shadowverse SkinPackManager not detected, skipping skin limit patch");
            return;
        }

        _prefsField = _skinMgrType.GetField("_preferences",
            BindingFlags.NonPublic | BindingFlags.Static);

        // SetEnabled(string packId, bool enabled) → 跳过 enabled && count >= 14 检查
        var setEnabledMethod = AccessTools.Method(_skinMgrType, "SetEnabled");
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                prefix: new HarmonyMethod(typeof(ShadowverseSkinLimitPatch), nameof(SetEnabled_Prefix)));
            Log.Info($"[{ModId}] Shadowverse SkinLimit: SetEnabled cap removed (14→unlimited)");
        }
    }

    /// <summary>
    /// 当启用皮肤时，跳过上限检查，直接写入 _preferences。
    /// 禁用操作仍走原方法。
    /// </summary>
    private static bool SetEnabled_Prefix(string packId, bool enabled, ref bool __result)
    {
        if (!enabled) return true; // 禁用走原方法

        var prefs = (Dictionary<string, bool>)_prefsField!.GetValue(null)!;
        prefs[packId] = true;
        __result = true;
        return false; // 跳过原方法
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