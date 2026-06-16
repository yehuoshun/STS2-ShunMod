using System.Text.Json;
using Godot;

namespace STS2ShunMod.STS2_ShunModCode.Settings;

/// <summary>
/// 配置持久化管理器。
/// 学 ModConfig 架构：按 key 分文件存 JSON，Save 有 debounce。
/// </summary>
internal static class SettingsManager
{
    private const string ConfigDir = "user://ShunMod/";
    private static readonly Dictionary<string, object> _values = new();
    private static readonly HashSet<string> _dirtyKeys = new();
    private static bool _saveScheduled;

    internal static event Action<string, object>? ValueChanged;

    internal static void Initialize()
    {
        DirAccess.MakeDirRecursiveAbsolute(ConfigDir);
    }

    internal static T GetValue<T>(string key, T fallback)
    {
        if (_values.TryGetValue(key, out var cached))
        {
            try { return (T)Convert.ChangeType(cached, typeof(T)); }
            catch { /* fall through */ }
        }

        var path = ConfigDir + key + ".json";
        if (!Godot.FileAccess.FileExists(path))
        {
            _values[key] = fallback!;
            return fallback;
        }

        try
        {
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var value = JsonSerializer.Deserialize<T>(json);
            _values[key] = value!;
            return value!;
        }
        catch
        {
            _values[key] = fallback!;
            return fallback;
        }
    }

    internal static void SetValue(string key, object value)
    {
        _values[key] = value;
        ValueChanged?.Invoke(key, value);
        ScheduleSave(key);
    }

    // ─── Debounce：合并到下一帧批量写 ───────────────────────────

    private static void ScheduleSave(string key)
    {
        _dirtyKeys.Add(key);
        if (_saveScheduled) return;
        _saveScheduled = true;

        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame += FlushSaves;
    }

    private static void FlushSaves()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame -= FlushSaves;
        _saveScheduled = false;

        foreach (var key in _dirtyKeys)
        {
            try
            {
                var path = ConfigDir + key + ".json";
                using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
                file.StoreString(JsonSerializer.Serialize(_values[key]));
            }
            catch { /* 写入失败不崩溃 */ }
        }
        _dirtyKeys.Clear();
    }
}
