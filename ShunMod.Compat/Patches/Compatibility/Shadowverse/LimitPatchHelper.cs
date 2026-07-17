using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core.Core;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
///     限制解除补丁的共享实现，合并 SkinLimit(14) 和 BgLimit(7) 的重复代码。
///     设计选择：
///     - Transpiler 而非 Prefix：SetEnabled 内部涉及 _preferences 字典修改和 LoadPack，
///     Prefix 跳过需要手动反射操作_ preferences，耦合内部实现。Transpiler 只替换上限常量，
///     原方法逻辑完整保留。
///     - AssemblyLoad 延迟加载：ShunMod 可能排在 Shadowverse 前面，Apply 时目标 DLL 可能
///     尚未加载，订阅事件兜底。
///     - AppliedFlag 而非 ref bool：局部函数捕获 ref 参数不被 C# 允许，引用类型包装解决。
/// </summary>
internal static class LimitPatchHelper
{
    public static void Apply(
        Harmony harmony,
        string modId,
        string targetNs,
        string targetType,
        string friendlyName,
        AppliedFlag applied,
        Type transpilerSource,
        string scanTranspilerName,
        string setEnabledTranspilerName)
    {
        var managerType = FindType(targetNs, targetType);
        if (managerType != null)
        {
            ApplyPatches(harmony, managerType, modId, friendlyName, applied,
                transpilerSource, scanTranspilerName, setEnabledTranspilerName);
            return;
        }

        Log.Info($"[{modId}] {friendlyName} not yet loaded, subscribing to AssemblyLoad...");
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        return;

        void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            if (applied.Value) return;
            if (FindType(targetNs, targetType) is not { } t) return;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            ApplyPatches(harmony, t, modId, friendlyName, applied,
                transpilerSource, scanTranspilerName, setEnabledTranspilerName);
        }
    }

    private static void ApplyPatches(
        Harmony harmony,
        Type managerType,
        string modId,
        string friendlyName,
        AppliedFlag applied,
        Type transpilerSource,
        string scanTranspilerName,
        string setEnabledTranspilerName)
    {
        if (Interlocked.CompareExchange(ref applied.Value, true, false)) return;

        Log.Info($"[{modId}] {friendlyName}: applying patches to {managerType.FullName}");

        var scanMethod = AccessTools.Method(managerType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(transpilerSource, scanTranspilerName));
            Log.Info($"[{modId}] {friendlyName}: ScanInstalledPacks (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{modId}] {friendlyName}: ScanInstalledPacks method not found!");
        }

        var setEnabledMethod = AccessTools.Method(managerType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(transpilerSource, setEnabledTranspilerName));
            Log.Info($"[{modId}] {friendlyName}: SetEnabled (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{modId}] {friendlyName}: SetEnabled method not found!");
        }
    }

    public static IEnumerable<CodeInstruction> ReplaceLimitConstant(
        IEnumerable<CodeInstruction> instructions,
        Func<CodeInstruction, bool> isConstant)
    {
        foreach (var inst in instructions)
        {
            if (isConstant(inst))
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = int.MaxValue;
            }

            yield return inst;
        }
    }

    public static bool IsConstant7(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S || inst.opcode == OpCodes.Ldc_I4)
               && inst.operand is 7;
    }

    public static bool IsConstant14(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S || inst.opcode == OpCodes.Ldc_I4)
               && inst.operand is 14;
    }

    private static Type? FindType(string ns, string typeName)
    {
        return CompatibilityPatchUtil.FindType(ns, typeName);
    }

    internal sealed class AppliedFlag
    {
        public bool Value;
    }
}