using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 自定义事件注册 — 将事件注入游戏事件列表。
/// </summary>
public static class EventRegistry
{
    private static readonly List<EventModel> Events = [];
    private static bool _registered;

    public static void Register(EventModel eventModel)
    {
        if (!Events.Contains(eventModel))
            Events.Add(eventModel);
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
public static class AllSharedEvents_Inject
{
    static IEnumerable<EventModel> Postfix(IEnumerable<EventModel> __result)
    {
        return [.. __result, .. EventRegistry.Events];
    }
}
