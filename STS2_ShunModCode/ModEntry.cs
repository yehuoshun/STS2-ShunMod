using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2ShunMod.STS2_ShunModCode.Core;
using STS2ShunMod.STS2_ShunModCode.Patches.Events;
using STS2ShunMod.STS2_ShunModCode.Settings;

namespace STS2ShunMod.STS2_ShunModCode;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private const string HarmonyId = "STS2ShunMod";
    private static readonly object Lock = new();
    private static bool _initialized;
    private static Harmony? _harmony;

    /// <summary>从 assets/STS2_ShunMod.json 读取模组 ID，缓存</summary>
    private static string? _modId;
    private static string ModId => _modId ??= ReadModId();

    private static string ReadModId()
    {
        try
        {
            var jsonRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "STS2_ShunMod.json");
            // Godot 导出后资源在 res:// 下，PCK 内路径不同；JSON 由 ModHelper 解析
            // 回退到硬编码，保证总是有值
            if (!File.Exists(jsonRoot)) return "STS2_ShunMod";
            var json = File.ReadAllText(jsonRoot);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() ?? "STS2_ShunMod" : "STS2_ShunMod";
        }
        catch
        {
            return "STS2_ShunMod";
        }
    }

    public static void Initialize()
    {
        lock (Lock)
        {
            if (_initialized) return;
            _initialized = true;
        }

        var id = ModId;
        Log.Info($"[{id}] ============================================================");
        Log.Info($"[{id}] Initializing {id}");

        // Phase 1: Harmony patches
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
                Log.Error($"[{id}]   → inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
        }

        // Phase 2: Settings (load before content registration so patches can read config)
        SettingsManager.Initialize();
        PatchManager.LoadFromSettings();
        SettingsUI.Initialize(PatchManager.GetConfigEntries());
        Log.Info($"[{id}] Settings system initialized");

        // Phase 3: Content registration (auto-scan [CardPool] / [RelicPool] / [EventPool])
        ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());

        Log.Info($"[{id}] Initialization complete");
        Log.Info($"[{id}] ============================================================");
    }
}
