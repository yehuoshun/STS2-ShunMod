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
        try
        {
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Info($"[{ModId}] Harmony patches installed successfully");
        }
        catch (Exception e)
        {
            Log.Error($"[{ModId}] Harmony patching failed: {e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                Log.Error($"[{ModId}]   \u2192 inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
        }

        // 超限记账 Power 不显式 Inject：ModelDb.Init() 的 AllAbstractModelSubtypes 会通过
        // ReflectionHelper.GetSubtypesInMods 自动扫描 mod 程序集中的 AbstractModel 子类并实例化注册。
        // 显式 Inject 会抢先注册 → Init 再次实例化时 AbstractModel 构造器抛 DuplicateModelException（启动崩溃）。
        Log.Info($"[{ModId}] OverflowPower / OverflowLifePower 由 ModelDb.Init 自动注册");

        Log.Info($"[{ModId}] Initialization complete");
        Log.Info($"[{ModId}] ============================================================");
    }
}