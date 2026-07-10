using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core;

namespace ShunMod.Compat.Patches.Compatibility.Shadowverse;

/// <summary>
/// 影之诗模组兼容 — 进化点系统全解除。
///
/// ════════════════════════════════════════════════════════════
///  设计原因
/// ════════════════════════════════════════════════════════════
///
///  为什么用 Prefix 跳过原方法，而不是 Transpiler/Postfix 覆写？
///  ───────────────────────────────────────────────────────────
///  TryUseEvolvePoint 的原实现是"检查点数 → 减 1 → 返回 true/false"。
///  我们要的效果是"不减 1、永远成功"。Prefix 返回 false 跳过原方法体，
///  是最直接的方式。Transpiler 需要定位 IL 中的减法指令，Postfix 覆写
///  __result 能解决"返回 true"但无法阻止点数递减——原方法已经执行完了。
///  Prefix 返回 false 是唯一既跳过减法又控制返回值的方案。
///
///  为什么 patch MarkEvolveUsedThisTurn 而不是 GetEvolveUsedThisTurn？
///  ───────────────────────────────────────────────────────────────
///  有些卡牌和机制需要通过 GetEvolveUsedThisTurn 检测"本回合是否进化过"
///  来触发效果。如果直接 patch Get 方法永久返回 false，这些检测全失效。
///  改为 patch Mark 方法，阻止 player 被加入 HashSet，这样：
///    1. MarkEvolveUsedThisTurn 被跳过 → HashSet 不记录
///    2. GetEvolveUsedThisTurn 自然返回 false → 没人阻止进化
///    3. 其他卡牌主动调 GetEvolveUsedThisTurn 仍能得到正确结果
///
///  为什么需要 Initialize_Prefix？进化点反正不消耗。
///  ───────────────────────────────────────────────────────────────
///  进化点不消耗只是我们的 patch 行为，游戏 UI 可能会根据 GetPoints()
///  的返回值来决定是否显示进化按钮、高亮进化入口等。设为 1 点确保 UI
///  正常显示"玩家有进化点可用"。不设 0 点是因为某些 UI 组件在 0 点时
///  可能直接隐藏进化入口，导致玩家找不到进化按钮。
///
///  为什么不用 PatchMethod 泛化到所有补丁？只在当前文件用。
///  ───────────────────────────────────────────────────────────────
///  PatchMethod 是本地辅助，只服务于此文件的 5 个 patch 调用。
///  每个 patch 类型不同（有的需要 Prefix 跳过，有的控制返回值），
///  泛化到所有兼容补丁反而增加耦合。SkinLimit 和 BgLimit 有各自的
///  LimitPatchHelper 共享模式，场景不同不混用。
///
/// ════════════════════════════════════════════════════════════
///  反编译确认的源码结构
/// ════════════════════════════════════════════════════════════
///
///  EvolutionPointManager 的关键字段：
///    _points = Dictionary{Player, (int evolve, int superEvolve)}
///    _evolveUsedThisTurn = HashSet{Player}     ← 进化标记（不是 bool 字段！）
///    _superEvolveUsedThisTurn = HashSet{Player}
///    _totalEvolveUsed = Dictionary{Player, int}
///
///  进化流程：
///    1. Initialize(player, 2, 2)       → 设置初始进化点
///    2. TryUseEvolvePoint(player)      → 检查 + 消耗（被 Patch 跳过）
///    3. 成功后调用 MarkEvolveUsedThisTurn → 标记本回合进化过（被 Patch 跳过）
///    4. 下次进化前调 GetEvolveUsedThisTurn → 检查标记（未标记 → 自然返回 false）
/// </summary>
public static class ShadowverseEvolutionPointPatch
{
    private const string ModId = "STS2ShunMod";
    private const string TargetNs = "shadowverse.Scripts";
    private const string TargetType = "EvolutionPointManager";

    public static void Apply(Harmony harmony)
    {
        // 跨程序集查找 EvolutionPointManager，找不到直接跳过（模组未安装）。
        // 不抛异常，不打错误日志，只打一条 info 表示"未检测到该模组"。
        var evoType = CompatibilityPatchUtil.FindPatchType(ModId, TargetNs, TargetType);
        if (evoType == null) return;

        // ── Patch 1: Initialize(player, evolvePoints=2, superEvolvePoints=2) ──
        // 默认参数由编译器在调用点内联，Prefix 用 ref int 截获改为 1。
        // 设 1 而不是 0，因为 UI 可能根据点数值决定是否高亮进化入口。
        PatchMethod(harmony, evoType, "Initialize",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Initialize_Prefix));

        // ── Patch 2: TryUseEvolvePoint(player) → bool ──
        // Prefix 跳过原方法，__result=true，进化不消耗点数。
        PatchMethod(harmony, evoType, "TryUseEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));

        // ── Patch 3: TryUseSuperEvolvePoint(player) → bool ──
        // 逻辑同 TryUseEvolvePoint，共用一个 Prefix 方法。
        PatchMethod(harmony, evoType, "TryUseSuperEvolvePoint",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(TryUse_Prefix));

        // ── Patch 4: MarkEvolveUsedThisTurn(player) ──
        // 不 patch GetEvolveUsedThisTurn（保留给其他卡牌做进化检测）。
        // patch 标记方法，阻止 player 被加入 _evolveUsedThisTurn HashSet。
        // GetEvolveUsedThisTurn 自然返回 false，无需覆写。
        PatchMethod(harmony, evoType, "MarkEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));

        // ── Patch 5: MarkSuperEvolveUsedThisTurn(player) ──
        PatchMethod(harmony, evoType, "MarkSuperEvolveUsedThisTurn",
            prefixType: typeof(ShadowverseEvolutionPointPatch),
            prefixName: nameof(Skip_Prefix));
    }

    // ═══════════════════════════════════════════════
    //  Harmony 方法
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Initialize Prefix — 把初始进化点从 2 改为 1。
    /// 编译器在调用点内联了默认值 (evolve=2, superEvolve=2)，ref 参数可以截获。
    /// 设 1 而不是 0，因为 UI 可能根据 GetPoints() 返回值决定是否显示进化入口。
    /// 实际进化不消耗点数，1 点够用整局。
    /// </summary>
    private static void Initialize_Prefix(ref int evolvePoints, ref int superEvolvePoints)
    {
        evolvePoints = 1;
        superEvolvePoints = 1;
    }

    /// <summary>
    /// TryUseEvolvePoint / TryUseSuperEvolvePoint Prefix — 跳过原方法，进化始终成功。
    /// Harmony Prefix 返回 false 时跳过原方法体，__result 设为 true。
    /// 原方法中"检查点数 → 减 1"的逻辑不会执行，实现"不消耗点数"。
    /// </summary>
    private static bool TryUse_Prefix(ref bool __result)
    {
        __result = true;
        return false; // 跳过原方法 → 进化点不递减
    }

    /// <summary>
    /// MarkEvolveUsedThisTurn / MarkSuperEvolveUsedThisTurn Prefix — 跳过标记。
    /// 阻止 player 被加入 _evolveUsedThisTurn / _superEvolveUsedThisTurn HashSet。
    /// 这样 GetEvolveUsedThisTurn 自然返回 false（未在集合中），
    /// 保留给其他卡牌做进化检测，同时解除回合限制。
    /// </summary>
    private static bool Skip_Prefix()
    {
        return false; // 跳过原方法 → 不标记进化状态
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 安全地给 Harmony 加补丁。方法不存在时只打 Warn 日志，不抛异常。
    /// 这样如果 Shadowverse 模组更新了方法名，补丁静默跳过，不会炸游戏。
    /// </summary>
    private static void PatchMethod(Harmony harmony, Type type, string methodName,
        Type? prefixType = null, string? prefixName = null,
        Type? postfixType = null, string? postfixName = null)
    {
        var method = AccessTools.Method(type, methodName);
        if (method == null)
        {
            Log.Warn($"[{ModId}] EvolutionPoint: {methodName} method not found!");
            return;
        }

        harmony.Patch(method,
            prefix: prefixType != null ? new HarmonyMethod(prefixType, prefixName!) : null,
            postfix: postfixType != null ? new HarmonyMethod(postfixType, postfixName!) : null);

        Log.Info($"[{ModId}] EvolutionPoint: {methodName} patched");
    }
}