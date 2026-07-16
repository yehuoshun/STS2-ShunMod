using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ShunMod.Tweaks;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private const string HarmonyId = "ShunMod_Tweaks";
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

        Log.Info($"[{HarmonyId}] ============================================================");
        Log.Info($"[{HarmonyId}] Initializing {HarmonyId}");

        _harmony = new Harmony(HarmonyId);
        try
        {
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Info($"[{HarmonyId}] Harmony patches installed successfully");
        }
        catch (Exception e)
        {
            Log.Error($"[{HarmonyId}] Harmony patching failed: {e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                Log.Error($"[{HarmonyId}]   \u2192 inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
        }

        Log.Info($"[{HarmonyId}] Initialization complete");
        Log.Info($"[{HarmonyId}] ============================================================");
    }
}
