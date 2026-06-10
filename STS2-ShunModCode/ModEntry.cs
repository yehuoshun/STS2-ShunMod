using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2ShunMod.Cards;
using STS2ShunMod.Relics;
using STS2ShunMod.Patches.Events;

namespace STS2ShunMod;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private const string HarmonyId = "STS2ShunMod";
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

        Log.Info($"[{ModInfo.Id}] ============================================================");
        Log.Info($"[{ModInfo.Id}] Starting initialization for game version {ModInfo.TargetGameVersion}");

        // Phase 1: Harmony patches
        _harmony = new Harmony(HarmonyId);
        try
        {
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Info($"[{ModInfo.Id}] Harmony patches installed successfully");
        }
        catch (Exception e)
        {
            Log.Error($"[{ModInfo.Id}] Harmony patching failed: {e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                Log.Error($"[{ModInfo.Id}]   → inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
        }

        // Phase 2: Content registration
        RegisterContent();

        Log.Info($"[{ModInfo.Id}] Initialization complete");
        Log.Info($"[{ModInfo.Id}] ============================================================");
    }

    private static void RegisterContent()
    {
        // Cards
        try
        {
            ModHelper.AddModelToPool(typeof(ColorlessCardPool), typeof(ShunModSuperApotheosis));
            Log.Info($"[{ModInfo.Id}] Registered card: ShunModSuperApotheosis → ColorlessCardPool");
        }
        catch (Exception e) { Log.Error($"[{ModInfo.Id}] Card registration failed: {e.Message}"); }

        // Relics
        try
        {
            ModHelper.AddModelToPool(typeof(SharedRelicPool), typeof(ShunModBossTrophy));
            Log.Info($"[{ModInfo.Id}] Registered relic: ShunModBossTrophy → SharedRelicPool");
        }
        catch (Exception e) { Log.Error($"[{ModInfo.Id}] Relic registration failed (BossTrophy): {e.Message}"); }

        try
        {
            ModHelper.AddModelToPool(typeof(SharedRelicPool), typeof(ShunModBountifulFrond));
            Log.Info($"[{ModInfo.Id}] Registered relic: ShunModBountifulFrond → SharedRelicPool");
        }
        catch (Exception e) { Log.Error($"[{ModInfo.Id}] Relic registration failed (BountifulFrond): {e.Message}"); }

        // Events — registered via ShunModEventRegistry + ModelDbInit_SafePatch
        try
        {
            ShunModEventRegistry.RegisterEventTypes(Assembly.GetExecutingAssembly());
            Log.Info($"[{ModInfo.Id}] Event types registered for SafeInit");
        }
        catch (Exception e) { Log.Error($"[{ModInfo.Id}] Event registration failed: {e.Message}"); }
    }
}