using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility;

/// <summary>
/// 影之诗模组兼容 — 解除背景包启用数量限制（7→无限）。
///
/// ════════════════════════════════════════════════════════════
///  设计原因
/// ════════════════════════════════════════════════════════════
///
///  为什么用 Transpiler 而不是 Prefix？
///  ───────────────────────────────────
///  SetEnabled 原方法内部逻辑不止是检查上限——还要修改 _preferences 字典、
///  加载未装载的背景包（LoadPack）。如果直接用 Prefix 跳过整个方法，
///  需要手动反射操作 _preferences，耦合内部实现、容易漏掉副效应。
///  Transpiler 只替换上限常量 7 为 int.MaxValue，原方法的所有逻辑完整保留。
///  (之前版本确实用了 Prefix，后来改成了 Transpiler)
///
///  为什么用 Interlocked 而不是 lock？
///  ───────────────────────────────────
///  _applied 守卫只有一处写入（ApplyPatches 入口）和一处读取（OnAssemblyLoad
///  快速路径）。Interlocked.CompareExchange 原子操作足以保证单次执行语义，
///  不需要引入锁。OnAssemblyLoad 的 if (_applied) 是快速路径优化——即使竞态
///  通过，ApplyPatches 的 CompareExchange 会兜底，不会重复打补丁。
///
///  为什么需要 AssemblyLoad 延迟加载？
///  ───────────────────────────────────
///  sts2 的模组加载顺序按字母排序，"ShunMod" 可能排在 "Shadowverse" 前面，
///  导致 Apply() 执行时 Shadowverse 的 DLL 尚未加载到 AppDomain 中。
///  AssemblyLoad 事件兜底确保 DLL 加载后自动补打补丁。
///
/// ════════════════════════════════════════════════════════════
///  反编译确认的限制点
/// ════════════════════════════════════════════════════════════
///
///  ScanInstalledPacks（启动加载）：
///    bool flag6 = flag5 && num >= 7;
///    if (flag6) { flag5 = false; Log.Warn(...AppendFormatted(7)...); }
///
///  SetEnabled（运行时 UI 开关）：
///    bool flag3 = BgPackManager.GetEnabledCount() >= 7;
///    if (flag3) { Log.Warn(...AppendFormatted(7)...); return false; }
///
///  Transpiler 同时替换两处 7：比较操作 + 日志字符串中的 AppendFormatted(7)。
///  日志文字会变成 "超出上限 2147483647"，不影响功能。
/// </summary>
public static class ShadowverseBgLimitPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts.UI";
    private const string TargetType = "BgPackManager";

    /// <summary>
    /// 是否已打补丁。Interlocked.CompareExchange 原子守卫，非 volatile。
    /// OnAssemblyLoad 快速路径的 if (_applied) 是竞态允许的优化——
    /// 即使多个线程同时读 false，ApplyPatches 的 CompareExchange 保证只执行一次。
    /// </summary>
    private static bool _applied;

    /// <summary>
    /// 入口。优先尝试直接查找 BgPackManager 类型并打补丁；
    /// 如果 DLL 尚未加载，延迟到 AssemblyLoad 事件触发时再打。
    /// </summary>
    public static void Apply(Harmony harmony)
    {
        // ── 优先路径：类型已加载，直接打补丁 ──
        var bgMgrType = FindType();
        if (bgMgrType != null)
        {
            ApplyPatches(harmony, bgMgrType);
            return;
        }

        // ── 延迟路径：模组按字母序加载，"ShunMod" 可能先于 "Shadowverse" 加载 ──
        // 订阅 AssemblyLoad 事件，等 Shadowverse 的 DLL 进来后再补打。
        Log.Info($"[{ModId}] Shadow verse BgPackManager not yet loaded, subscribing to AssemblyLoad...");
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        return;

        // 局部函数，捕获 harmony 闭包
        void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            // 快速路径：已打过的直接跳过（竞态安全，ApplyPatches 有原子守卫兜底）
            if (_applied) return;
            if (FindType() is { } t)
            {
                // 取消订阅，防止重复触发
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                ApplyPatches(harmony, t);
            }
        }
    }

    /// <summary>
    /// 对已找到的 BgPackManager 类型应用 Transpiler 补丁。
    /// 线程安全：Interlocked.CompareExchange 保证仅第一个调用者执行补丁逻辑。
    /// </summary>
    private static void ApplyPatches(Harmony harmony, Type bgMgrType)
    {
        // ── 原子守卫 ──
        // CompareExchange(ref _applied, true, false)：
        //   如果 _applied == false，设置为 true，返回 false（未应用过，继续）
        //   如果 _applied == true，不动，返回 true（已应用过，跳过）
        // 相比 lock 方案：无锁开销，无死锁风险，分析器不报"同步块不一致"。
        if (Interlocked.CompareExchange(ref _applied, true, false)) return;

        Log.Info($"[{ModId}] Shadow verse BgLimit: applying patches to {bgMgrType.FullName}");

        // ── Patch 1: ScanInstalledPacks ──
        // 启动加载时扫描背景包目录，发现已启用数量 >= 7 则强制禁用后续包。
        // Transpiler 将 7 替换为 int.MaxValue，使上限检查永不触发。
        var scanMethod = AccessTools.Method(bgMgrType, "ScanInstalledPacks");
        if (scanMethod != null)
        {
            harmony.Patch(scanMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(ScanInstalledPacks_Transpiler)));
            Log.Info($"[{ModId}] Shadow verse BgLimit: ScanInstalledPacks (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{ModId}] Shadow verse BgLimit: ScanInstalledPacks method not found!");
        }

        // ── Patch 2: SetEnabled ──
        // UI 点击启用时，检查 GetEnabledCount() >= 7 则拒绝并返回 false。
        // Transpiler 将 7 替换为 int.MaxValue，使上限检查永不触发。
        // 原方法剩余逻辑（写 _preferences、加载未装载包）完整保留。
        var setEnabledMethod = AccessTools.Method(bgMgrType, "SetEnabled",
            [typeof(string), typeof(bool)]);
        if (setEnabledMethod != null)
        {
            harmony.Patch(setEnabledMethod,
                transpiler: new HarmonyMethod(typeof(ShadowverseBgLimitPatch),
                    nameof(SetEnabled_Transpiler)));
            Log.Info($"[{ModId}] Shadow verse BgLimit: SetEnabled (Transpiler, unlimited)");
        }
        else
        {
            Log.Warn($"[{ModId}] Shadow verse BgLimit: SetEnabled method not found!");
        }
    }

    // ═══════════════════════════════════════════════
    //  Transpiler
    // ═══════════════════════════════════════════════

    /// <summary>
    /// ScanInstalledPacks 的 Transpiler。
    /// 将 IL 中所有常量 7 替换为 int.MaxValue，
    /// 覆盖：num >= 7 的比较 + AppendFormatted(7) 的日志参数。
    /// </summary>
    private static IEnumerable<CodeInstruction> ScanInstalledPacks_Transpiler(
        IEnumerable<CodeInstruction> instructions) => ReplaceLimitConstant(instructions);

    /// <summary>
    /// SetEnabled 的 Transpiler。
    /// 将 IL 中所有常量 7 替换为 int.MaxValue，
    /// 覆盖：GetEnabledCount() >= 7 的比较 + AppendFormatted(7) 的日志参数。
    /// 原方法其他逻辑（修改偏好、加载包体）不受影响。
    /// </summary>
    private static IEnumerable<CodeInstruction> SetEnabled_Transpiler(
        IEnumerable<CodeInstruction> instructions) => ReplaceLimitConstant(instructions);

    // ═══════════════════════════════════════════════
    //  核心替换逻辑
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 遍历 IL 指令，将 7 的常量压入替换为 int.MaxValue。
    /// 为什么全部替换而不是定位特定位置？
    ///   本方法中 7 仅出现在两个地方——比较操作和 AppendFormatted 日志参数。
    ///   全部替换是安全的，不影响其他逻辑。
    ///   如果未来游戏更新在方法中引入其他 7 常量，全局替换也自动覆盖。
    /// </summary>
    private static IEnumerable<CodeInstruction> ReplaceLimitConstant(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var inst in instructions)
        {
            if (IsConstant7(inst))
            {
                inst.opcode = OpCodes.Ldc_I4;
                inst.operand = int.MaxValue;
            }
            yield return inst;
        }
    }

    /// <summary>
    /// 判断 IL 指令是否为上限常量 7。
    /// ldc.i4.s（短格式，≤127）→ 7 的 IL 表示
    /// ldc.i4（长格式，>127）→ 7 的 IL 表示（C# 编译器可能选择任一种）
    /// 两种格式都匹配，不遗漏。
    /// 背景包上限只有 7，没有第二种常量（对比 SkinLimit 有 14 和 140 两种）。
    /// </summary>
    private static bool IsConstant7(CodeInstruction inst)
    {
        return (inst.opcode == OpCodes.Ldc_I4_S && inst.operand is sbyte and 7)
            || (inst.opcode == OpCodes.Ldc_I4 && inst.operand is int and 7);
    }

    /// <summary>
    /// 跨程序集查找 BgPackManager 类型。不引用 shadowverse.dll，纯反射。
    /// </summary>
    private static Type? FindType() => CompatibilityPatchUtil.FindType(TargetNs, TargetType);
}