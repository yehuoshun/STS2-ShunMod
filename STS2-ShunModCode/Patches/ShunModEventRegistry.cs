using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Patches;

/// <summary>
///     自定义事件注册 — 收集 EventModel 实例并在 ModelDb.Init 时注册，
///     通过 Harmony 注入 AllSharedEvents。
///     参照 YuWanCard CustomEventRegistry。
/// </summary>
public static class ShunModEventRegistry
{
    /// <summary>
    ///     非 act 限定事件，注入 AllSharedEvents。
    /// </summary>
    public static readonly List<EventModel> SharedEvents = [];

    /// <summary>
    ///     在 ModelDb.Init 期间调用，从 ContentRegistry.EventTypes 创建实例并注册。
    /// </summary>
    internal static void CreateInstancesFromEventTypes()
    {
        foreach (var type in ContentRegistry.EventTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is EventModel eventModel)
                    SharedEvents.Add(eventModel);
            }
            catch
            {
                // 跳过无法实例化的类型
            }
        }
    }
}

/// <summary>
///     劫持 ModelDb.Init，在初始化期间创建自定义事件实例。
///     参照 YuWanCard InitDeDuplicationPatch。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyPriority(Priority.Last)]
static class ModelDbInit_EventPatch
{
    [HarmonyPostfix]
    static void CreateCustomEvents()
    {
        ShunModEventRegistry.CreateInstancesFromEventTypes();
    }
}

/// <summary>
///     将 ShunModEventRegistry.SharedEvents 注入 ModelDb.AllSharedEvents。
///     参照 YuWanCard AllSharedEventsPatch。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
static class AllSharedEvents_ShunModPatch
{
    [HarmonyPostfix]
    static IEnumerable<EventModel> AddCustomEvents(IEnumerable<EventModel> __result)
    {
        return [.. __result ?? [], .. ShunModEventRegistry.SharedEvents];
    }
}
