using System.Collections.Generic;
using System.IO;
using Godot;
using Newtonsoft.Json;

namespace STS2_ShunMod.Abilities;

/// <summary>能力配置定义，从 abilities.json 加载</summary>
public record AbilityDef(string name, string desc, int per_stack);

/// <summary>能力配置加载器 — 从 .pck 包读取</summary>
public static class AbilityConfig
{
    private static Dictionary<string, AbilityDef>? _cache;

    public static Dictionary<string, AbilityDef> Load()
    {
        if (_cache != null) return _cache;

        using var file = FileAccess.Open("res://STS2_ShunMod/abilities.json", FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        _cache = JsonConvert.DeserializeObject<Dictionary<string, AbilityDef>>(json)!;
        return _cache;
    }
}
