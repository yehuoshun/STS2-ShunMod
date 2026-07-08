using HarmonyLib;

namespace ShunMod.Compat;

/// <summary>
/// 第三方模组兼容补丁统一入口。
/// 包装各 Apply 调用，避免 ModEntry 初期化逻辑被兼容性代码打散。
/// 所有补丁均为反射实现，不硬依赖目标模组 DLL。
/// </summary>
internal static class CompatibilityPatches
{
    /// <summary>安装所有已注册的第三方模组兼容补丁。</summary>
    public static void ApplyAll(Harmony harmony)
    {
        // ── 在此追加新的兼容补丁 ──
        ShadowverseSkinLimitPatch.Apply(harmony);
        ShadowverseBgLimitPatch.Apply(harmony);
        ShadowverseEvolutionPointPatch.Apply(harmony);
        // ──────────────────────────
    }
}
