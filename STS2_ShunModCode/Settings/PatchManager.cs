namespace STS2ShunMod.STS2_ShunModCode.Settings;

/// <summary>
/// 补丁开关管理器。从 SettingsManager 加载初始值，Patch 中调用 IsEnabled() 检查。
/// </summary>
internal static class PatchManager
{
    private static readonly Dictionary<string, bool> _patchStates = new();
    public static float DamageMultiplier { get; set; } = 1.0f;

    /// <summary>在 Harmony Patch 中检查补丁是否启用</summary>
    internal static bool IsEnabled(string patchName)
    {
        return _patchStates.GetValueOrDefault(patchName, true);
    }

    internal static void SetEnabled(string patchName, bool enabled)
    {
        _patchStates[patchName] = enabled;
    }

    /// <summary>初始化时从 SettingsManager 加载所有开关状态</summary>
    internal static void LoadFromSettings()
    {
        _patchStates["InfiniteUpgrade"] = SettingsManager.GetValue("infinite_upgrade", true);
        _patchStates["EnergyRetention"] = SettingsManager.GetValue("energy_retention", true);
        _patchStates["BlockRetention"] = SettingsManager.GetValue("block_retention", true);
        _patchStates["ShowTotalDamage"] = SettingsManager.GetValue("show_total_damage", true);
        _patchStates["ForgePullBlades"] = SettingsManager.GetValue("forge_pull_blades", true);
        _patchStates["SpiralEnchant"] = SettingsManager.GetValue("spiral_enchant", true);
        _patchStates["HardenedShell"] = SettingsManager.GetValue("hardened_shell", true);
    }

    /// <summary>获取所有补丁开关配置项</summary>
    internal static ConfigEntry[] GetConfigEntries()
    {
        return new[]
        {
            new ConfigEntry
            {
                Key = "infinite_upgrade", Label = "无限升级",
                Type = ConfigEntryType.Toggle, DefaultValue = true,
                OnChanged = v => SetEnabled("InfiniteUpgrade", Convert.ToBoolean(v))
            },
            new ConfigEntry
            {
                Key = "energy_retention", Label = "能量保留（冰激凌）",
                Type = ConfigEntryType.Toggle, DefaultValue = true,
                OnChanged = v => SetEnabled("EnergyRetention", Convert.ToBoolean(v))
            },
            new ConfigEntry
            {
                Key = "block_retention", Label = "格挡保留",
                Type = ConfigEntryType.Toggle, DefaultValue = true,
                OnChanged = v => SetEnabled("BlockRetention", Convert.ToBoolean(v))
            },
            new ConfigEntry
            {
                Key = "show_total_damage", Label = "显示总伤害",
                Type = ConfigEntryType.Toggle, DefaultValue = true,
                OnChanged = v => SetEnabled("ShowTotalDamage", Convert.ToBoolean(v))
            },
            new ConfigEntry
            {
                Key = "forge_pull_blades", Label = "锻造拉回君王之剑",
                Type = ConfigEntryType.Toggle, DefaultValue = true,
                OnChanged = v => SetEnabled("ForgePullBlades", Convert.ToBoolean(v))
            },
            new ConfigEntry
            {
                Key = "spiral_enchant", Label = "附魔限制解锁",
                Type = ConfigEntryType.Toggle, DefaultValue = true,
                OnChanged = v => SetEnabled("SpiralEnchant", Convert.ToBoolean(v))
            },
            new ConfigEntry
            {
                Key = "hardened_shell", Label = "硬化外壳修复",
                Type = ConfigEntryType.Toggle, DefaultValue = true,
                OnChanged = v => SetEnabled("HardenedShell", Convert.ToBoolean(v))
            }
        };
    }
}