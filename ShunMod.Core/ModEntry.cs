using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ShunMod.Core;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private const string HarmonyId = "ShunMod_Core";
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

        var id = "ShunMod_Core";
        Log.Info($"[{id}] ============================================================");
        Log.Info($"[{id}] Initializing {id}");

        _harmony = new Harmony(HarmonyId);
        try
        {
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Info($"[{id}] Harmony patches installed successfully");
        }
        catch (Exception e)
        {
            Log.Error($"[{id}] Harmony patching failed: {e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                Log.Error($"[{id}]   \u2192 inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
        }

        Log.Info($"[{id}] Initialization complete");
        Log.Info($"[{id}] ============================================================");
    }
}
