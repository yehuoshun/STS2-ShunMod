using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     战斗内来源
/// </summary>
public enum PowerOrigin
{
    Battle,
    Event
}

/// <summary>
///     持久化的 Power 快照条目
/// </summary>
public readonly record struct PersistedPower(ModelId Id, int Amount);

/// <summary>
///     快照在内存中按玩家 NetId 保存，由 <see cref="PowersPersistStore" /> 负责落盘/读盘，
///     因此重进存档（乃至重启游戏）后仍能重连。
///     来源标记只在当前战斗内有意义，随战斗结束清空。
/// </summary>
public static class PersistTracker
{
    private static readonly Dictionary<ulong, List<PersistedPower>> Snapshots = new();
    private static readonly Dictionary<(ulong NetId, ModelId PowerId), PowerOrigin> Origins = new();

    /// <summary>
    ///     重连 Power 时设为 true，使 PowerCmd.Apply Postfix 跳过来源标记，
    ///     避免重连循环误将一切标记为 Event（此时 IsInProgress 仍为 false）。
    /// </summary>
    public static bool IsReapplying { get; set; }

    public static void SetSnapshot(ulong netId, List<PersistedPower> powers)
    {
        Snapshots[netId] = powers;
    }

    /// <summary>
    ///     非破坏性读取：快照保留到下一次战斗结束被覆盖，
    ///     这样读档重开同一场战斗时还能再重连一次。
    /// </summary>
    public static List<PersistedPower>? PeekSnapshot(ulong netId)
    {
        return Snapshots.TryGetValue(netId, out var snap) ? snap : null;
    }

    /// <summary>
    ///     当前全部快照（供落盘遍历）。
    /// </summary>
    public static IReadOnlyDictionary<ulong, List<PersistedPower>> AllSnapshots => Snapshots;

    /// <summary>
    ///     丢弃全部快照（换局 / 新开一局时调用，避免串档）。
    /// </summary>
    public static void ClearAll()
    {
        Snapshots.Clear();
        Origins.Clear();
    }

    public static void TagOrigin(ulong netId, ModelId powerId, PowerOrigin origin)
    {
        Origins[(netId, powerId)] = origin;
    }

    public static bool IsEventOrigin(ulong netId, ModelId powerId)
    {
        return Origins.TryGetValue((netId, powerId), out var origin)
            && origin == PowerOrigin.Event;
    }

    public static void ClearOriginsFor(ulong netId)
    {
        foreach (var key in Origins.Keys.ToArray())
            if (key.NetId == netId)
                Origins.Remove(key);
    }
}
