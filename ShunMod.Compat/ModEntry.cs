using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using ShunMod.Compat;

namespace ShunMod.Compat;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private const string HarmonyId = "ShunMod_Compat";
    private static readonly object Lock = new();
    private static bool _initialized;
    private static Harmony? _harmony;

    public static void Initialize()
    {
        lock (Lock)
        {
            if (_initialized) return;
            _initialized = true;
        }

        var id = "ShunMod_Compat";
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

        // Phase 2: Compatibility patches
        CompatibilityPatches.ApplyAll(_harmony);

        Log.Info($"[{id}] Initialization complete");
        Log.Info($"[{id}] ============================================================");
    }
}
