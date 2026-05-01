using System.Collections.Generic;
using System.IO;
using Godot;
using Newtonsoft.Json;

namespace STS2_ShunMod.Abilities;

/// <summary>
/// 能力本地存档 — 存到 user:// 目录，跨存档持久化。
/// </summary>
public static class AbilityStore
{
    private static string SavePath =>
        ProjectSettings.GlobalizePath("user://STS2_ShunMod/ability_data.json");

    /// <summary>保存能力数据到本地文件</summary>
    public static void Save(Dictionary<string, int> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        File.WriteAllText(SavePath, JsonConvert.SerializeObject(data));
    }

    /// <summary>从本地文件加载能力数据，不存在则返回空字典</summary>
    public static Dictionary<string, int> Load()
    {
        if (!File.Exists(SavePath)) return new();
        return JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(SavePath)) ?? new();
    }

    /// <summary>叠加指定能力</summary>
    public static void Add(string key, int amount)
    {
        var data = Load();
        data.TryGetValue(key, out var cur);
        data[key] = cur + amount;
        Save(data);
    }

    /// <summary>获取指定能力的层数</summary>
    public static int Get(string key)
    {
        var data = Load();
        return data.TryGetValue(key, out var v) ? v : 0;
    }
}
