using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Patches;

/// <summary>
///     自定义事件注册 — 从 ModelDb 取正规实例注入 AllSharedEvents。
///     参照 YuWanCard CustomEventRegistry + AllSharedEventsPatch。
/// </summary>
public static class ShunModEventRegistry
{
    /// <summary>
    ///     共享事件列表（非 act 限定），由 AllSharedEventsPatch 注入。
    /// </summary>
    public static readonly List<EventModel> SharedEvents = [];

    /// <summary>
    ///     注册事件实例。若非 act 限定事件，加入 SharedEvents。
    /// </summary>
    public static void Register(EventModel eventModel)
    {
        if (!SharedEvents.Contains(eventModel))
            SharedEvents.Add(eventModel);
    }
}

/// <summary>
///     将 ShunModEventRegistry.SharedEvents 注入 ModelDb.AllSharedEvents。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
internal static class AllSharedEvents_InjectPatch
{
    [HarmonyPostfix]
    private static IEnumerable<EventModel> Postfix(IEnumerable<EventModel> __result)
    {
        var merged = __result.Concat(ShunModEventRegistry.SharedEvents).ToList();
        return merged;
    }
}

/// <summary>
///     ModelDb.Init 后 — 从 ModelDb 取正规实例注册到 ShunModEventRegistry。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyPriority(Priority.Last)]
internal static class ModelDbInit_RegisterPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        var count = 0;
        foreach (var type in ContentRegistry.EventTypes)
        {
            var id = ModelDb.GetId(type);
            if (ModelDb.GetByIdOrNull<EventModel>(id) is EventModel em
                && !ShunModEventRegistry.SharedEvents.Contains(em))
            {
                ShunModEventRegistry.Register(em);
                count++;
            }
        }
    }
}