using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     战斗内来源
/// </summary>
public enum PowerOrigin
{
    Battle,
    Event,
}

/// <summary>
///     持久化的 Power 快照条目
/// </summary>
public readonly record struct PersistedPower(ModelId Id, int Amount);

/// <summary>
///     纯内存设计：快照和来源标记随进程销毁，
///     自然实现了"不保存到存档"的行为。
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

    public static List<PersistedPower>? TakeSnapshot(ulong netId)
    {
        if (!Snapshots.TryGetValue(netId, out var snap))
            return null;

        Snapshots.Remove(netId);
        return snap;
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
        Origins.Keys.Where(k => k.NetId == netId).ToList().ForEach(k => Origins.Remove(k));
    }
}