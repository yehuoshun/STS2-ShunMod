using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     把 <see cref="PersistTracker" /> 的内存快照落盘到 user:// 下（按 profile 区分），
///     让快照在「退出到主菜单 / 重启游戏后继续」时仍然存在。
///     <para>
///         游戏的存档（SerializableRun / SerializablePlayer / SerializableRoom）里没有 Power 字段，
///         读档进入战斗房又只是按 encounter 重建一场新战斗，所以重连所需的数据只能自己存。
///     </para>
///     快照用一个 run 归属标识（种子）做校验：种子对不上说明是上一局的残留，直接丢弃，
///     新开一局时也会清空，避免把上一局的能力带进新局。
/// </summary>
internal static class PowersPersistStore
{
    private const string LogPrefix = "[PowersPersist]";
    private const string DataDirName = "ShunMod";
    private const string FileName = "powers_persist.json";
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     当前内存快照所属的 run 种子；null 表示尚未确定（刚启动进程）。
    /// </summary>
    private static string? _seed;

    /// <summary>
    ///     战斗开始前调用：确保内存快照属于当前 run。种子变化（换档/换局）时丢弃内存快照，
    ///     再从磁盘按当前种子恢复。已对齐时是空操作，保证同一次战斗不会重复读盘。
    /// </summary>
    public static void EnsureRun(IRunState? runState)
    {
        var seed = GetSeed(runState);
        if (string.IsNullOrEmpty(seed))
            return;

        if (_seed == seed)
            return;

        PersistTracker.ClearAll();
        _seed = seed;
        Load(seed);
    }

    /// <summary>
    ///     战斗结束快照后调用：把内存快照写到磁盘（没有快照则清掉旧文件）。
    /// </summary>
    public static void Save(IRunState? runState)
    {
        var seed = GetSeed(runState);
        if (string.IsNullOrEmpty(seed))
            return;

        _seed = seed;
        Write(seed);
    }

    /// <summary>
    ///     新开一局时调用：丢弃内存快照并删除磁盘文件。
    /// </summary>
    public static void Reset()
    {
        PersistTracker.ClearAll();
        _seed = null;
        var path = TryGetFilePath();
        if (path == null)
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LogPrefix} 清理快照文件失败: {ex.Message}");
        }
    }

    private static void Load(string seed)
    {
        var path = TryGetFilePath();
        if (path == null || !File.Exists(path))
            return;

        try
        {
            var file = JsonSerializer.Deserialize<PersistFile>(File.ReadAllText(path));
            if (file == null)
                return;

            if (file.Version != SchemaVersion || !string.Equals(file.Seed, seed, StringComparison.Ordinal))
            {
                // 上一局（或旧版格式）残留：删掉，避免串档。
                File.Delete(path);
                return;
            }

            foreach (var player in file.Players)
            {
                var powers = player.Powers
                    .Where(power => !string.IsNullOrEmpty(power.Category) && !string.IsNullOrEmpty(power.Entry))
                    .Select(power => new PersistedPower(new ModelId(power.Category, power.Entry), power.Amount))
                    .ToList();

                if (powers.Count > 0)
                    PersistTracker.SetSnapshot(player.NetId, powers);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{LogPrefix} 读取快照文件失败: {ex}");
        }
    }

    private static void Write(string seed)
    {
        var path = TryGetFilePath();
        if (path == null)
            return;

        try
        {
            if (PersistTracker.AllSnapshots.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            var file = new PersistFile
            {
                Version = SchemaVersion,
                Seed = seed,
                Players = PersistTracker.AllSnapshots
                    .Select(pair => new PersistPlayer
                    {
                        NetId = pair.Key,
                        Powers = pair.Value
                            .Select(power => new PersistPower
                            {
                                Category = power.Id.Category,
                                Entry = power.Id.Entry,
                                Amount = power.Amount
                            })
                            .ToList()
                    })
                    .ToList()
            };

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error($"{LogPrefix} 写入快照文件失败: {ex}");
        }
    }

    /// <summary>
    ///     run 归属标识：种子随存档持久化，读档/重启后不变，换局则不同。
    /// </summary>
    private static string? GetSeed(IRunState? runState)
    {
        try
        {
            return runState?.Rng.StringSeed;
        }
        catch (Exception ex)
        {
            Log.Warn($"{LogPrefix} 读取 run 种子失败: {ex.Message}");
            return null;
        }
    }

    private static string? TryGetFilePath()
    {
        try
        {
            var profileId = SaveManager.Instance.CurrentProfileId;
            var dirPath = UserDataPathProvider.GetProfileScopedPath(profileId, DataDirName);
            return Path.Combine(ProjectSettings.GlobalizePath(dirPath), FileName);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LogPrefix} 无法定位快照目录，跳过落盘: {ex.Message}");
            return null;
        }
    }

    private sealed class PersistFile
    {
        [JsonPropertyName("version")] public int Version { get; set; }

        [JsonPropertyName("seed")] public string Seed { get; set; } = string.Empty;

        [JsonPropertyName("players")] public List<PersistPlayer> Players { get; set; } = new();
    }

    private sealed class PersistPlayer
    {
        [JsonPropertyName("net_id")] public ulong NetId { get; set; }

        [JsonPropertyName("powers")] public List<PersistPower> Powers { get; set; } = new();
    }

    private sealed class PersistPower
    {
        [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;

        [JsonPropertyName("entry")] public string Entry { get; set; } = string.Empty;

        [JsonPropertyName("amount")] public int Amount { get; set; }
    }
}
