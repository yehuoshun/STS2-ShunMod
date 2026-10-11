using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ShunMod.Tweaks;

[ModInitializer(nameof(Initialize))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class ModEntry
{
    private const string ModId = "ShunMod_Tweaks";
    private static readonly Lock Lock = new();
    private static bool _initialized;
    private static Harmony? _harmony;

    public static void Initialize()
    {
        lock (Lock)
        {
            if (_initialized) return;
            _initialized = true;
        }

        Log.Info($"[{ModId}] ============================================================");
        Log.Info($"[{ModId}] Initializing {ModId}");

        _harmony = new Harmony(ModId);
        // 逐个 patch 容错安装：某个 patch 目标在当前游戏版本失效（方法被删/改签名）时
        // 只跳过该 patch 并记日志，不再让 PatchAll 整体失败导致其余 patch 全部缺席。
        var failedPatches = new List<string>();
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
                     .SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Concat(new[] { t }))
                     .Where(t => t.GetCustomAttributes<HarmonyPatch>(false).Any()))
        {
            try
            {
                _harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception e)
            {
                failedPatches.Add($"{type.FullName}: {e.Message}");
            }
        }

        if (failedPatches.Count == 0)
        {
            Log.Info($"[{ModId}] Harmony patches installed successfully");
        }
        else
        {
            Log.Error($"[{ModId}] {failedPatches.Count} patch(es) failed to install（其余 patch 不受影响）:");
            foreach (var fail in failedPatches)
                Log.Error($"[{ModId}]   \u2717 {fail}");
        }

        // 超限记账 Power 不显式 Inject：ModelDb.Init() 的 AllAbstractModelSubtypes 会通过
        // ReflectionHelper.GetSubtypesInMods 自动扫描 mod 程序集中的 AbstractModel 子类并实例化注册。
        // 显式 Inject 会抢先注册 → Init 再次实例化时 AbstractModel 构造器抛 DuplicateModelException（启动崩溃）。
        Log.Info($"[{ModId}] OverflowPower / OverflowLifePower 由 ModelDb.Init 自动注册");

        Log.Info($"[{ModId}] Initialization complete");
        Log.Info($"[{ModId}] ============================================================");
    }
}