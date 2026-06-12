namespace STS2ShunMod.STS2_ShunModCode.Settings;

/// <summary>
/// 配置项定义。
/// </summary>
internal class ConfigEntry
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public ConfigEntryType Type { get; set; }
    public object DefaultValue { get; set; } = false;
    public float Min { get; set; }
    public float Max { get; set; } = 100f;
    public float Step { get; set; } = 1f;
    public string Format { get; set; } = "F0";
    public string[] Options { get; set; } = Array.Empty<string>();
    public Action<object>? OnChanged { get; set; }
}

internal enum ConfigEntryType
{
    Toggle,
    Slider,
    Dropdown
}
