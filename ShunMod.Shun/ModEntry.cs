using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using ShunMod.Core.Core.Registry;

namespace ShunMod.Shun;

[ModInitializer(nameof(Initialize))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class ModEntry
{
    private const string ModId = "ShunMod_Shun";
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

        // 本地化注册移至 EnchantLocalizationPatch（Harmony），等 LocManager 就绪后再执行
        // Content registration: 卡牌/遗物/事件
        ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());

        Log.Info($"[{ModId}] Initialization complete");
        Log.Info($"[{ModId}] ============================================================");
    }
}