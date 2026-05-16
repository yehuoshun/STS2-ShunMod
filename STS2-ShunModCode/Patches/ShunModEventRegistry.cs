using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
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
        if (eventModel.Acts.Length == 0)
            SharedEvents.Add(eventModel);
    }
}

/// <summary>
///     将 ShunModEventRegistry.SharedEvents 注入 ModelDb.AllSharedEvents。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
static class AllSharedEvents_InjectPatch
{
    [HarmonyPostfix]
    static IEnumerable<EventModel> Postfix(IEnumerable<EventModel> __result)
    {
        return [.. __result, .. ShunModEventRegistry.SharedEvents];
    }
}

/// <summary>
///     ModelDb.Init 后 — 从 ModelDb 取正规实例注册到 ShunModEventRegistry。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyPriority(Priority.Last)]
static class ModelDbInit_RegisterPatch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        foreach (var type in ContentRegistry.EventTypes)
        {
            var id = ModelDb.GetId(type);
            if (ModelDb.GetByIdOrNull<EventModel>(id) is EventModel em
                && !ShunModEventRegistry.SharedEvents.Contains(em))
            {
                ShunModEventRegistry.Register(em);
            }
        }
    }
}
