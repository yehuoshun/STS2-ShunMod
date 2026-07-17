using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using ShunMod.Compat.Patches.Compatibility;

namespace ShunMod.Compat;

[ModInitializer(nameof(Initialize))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class ModEntry
{
    /// <summary>ShunMod.Compat 日志前缀 / Harmony ID，供各模块统一引用。</summary>
    public const string ModId = "ShunMod_Compat";

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

        // Phase 2: Compatibility patches
        CompatibilityPatches.ApplyAll(_harmony);

        Log.Info($"[{ModId}] Initialization complete");
        Log.Info($"[{ModId}] ============================================================");
    }
}