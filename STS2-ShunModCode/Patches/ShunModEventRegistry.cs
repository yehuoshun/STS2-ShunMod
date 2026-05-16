using System.Runtime.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Patches;

/// <summary>
///     自定义事件实例缓存 — 懒加载创建，注入 AllSharedEvents。
/// </summary>
internal static class ShunModEventCache
{
    private static List<EventModel>? _cached;

    public static List<EventModel> Events
    {
        get
        {
            if (_cached != null) return _cached;

            _cached = [];
            foreach (var type in ContentRegistry.EventTypes)
            {
                EventModel? instance = null;
                try
                {
                    instance = Activator.CreateInstance(type, true) as EventModel;
                }
                catch
                {
                    // Activator.CreateInstance 失败 → 回退到 GetUninitializedObject
                    try
                    {
                        instance = FormatterServices.GetUninitializedObject(type) as EventModel;
                    }
                    catch { }
                }
                if (instance != null)
                    _cached.Add(instance);
            }
            return _cached;
        }
    }
}

/// <summary>
///     将自定义事件注入 AllSharedEvents。
///     参照 YuWanCard AllSharedEventsPatch。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
static class AllSharedEvents_ShunModPatch
{
    [HarmonyPostfix]
    static IEnumerable<EventModel> Postfix(IEnumerable<EventModel> __result)
    {
        return [.. __result ?? [], .. ShunModEventCache.Events];
    }
}

/// <summary>
///     ModelDb.Init 前缀 — 预热事件缓存。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyPriority(Priority.First)]
static class ModelDbInit_ShunModPatch
{
    [HarmonyPrefix]
    static void Prefix()
    {
        _ = ShunModEventCache.Events;
    }
}
