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
///     ModelDb.Init Prefix — 跳过原版 Init，改为 SafeInit 去重构造。
///     原版 Init 遍历 AllAbstractModelSubtypes 会触发 DuplicateModelException（Init_Patch1 重复构造）。
///     return false 跳过原版 Init，ExecuteEssential 中后续的 ModelIdSerializationCache.Init + InitIds 正常执行。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyPriority(Priority.First)]
internal static class ModelDbInit_SafePatch
{
    private static readonly System.Reflection.FieldInfo? ContentByIdField =
        typeof(ModelDb).GetField("_contentById",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (ContentByIdField?.GetValue(null) is not IDictionary<ModelId, AbstractModel> contentById)
        {
            Log.Warn("[STS2-ShunMod] _contentById 字段不可用，回退到原版 Init");
            return true;
        }

        var allTypes = ModelDb.AllAbstractModelSubtypes;
        var created = 0;

        foreach (var type in allTypes)
        {
            var id = ModelDb.GetId(type);
            if (contentById.ContainsKey(id))
                continue;

            var value = (AbstractModel)Activator.CreateInstance(type)!;
            contentById[id] = value;
            created++;
        }

        Log.Info($"[STS2-ShunMod] SafeInit: {allTypes.Length} 类型, {created} 新建, {contentById.Count - created} 已存在, {ContentRegistry.EventTypes.Count} 事件类型");

        // 注册 ShunMod 事件
        foreach (var type in ContentRegistry.EventTypes)
        {
            var id = ModelDb.GetId(type);
            if (contentById.TryGetValue(id, out var model) && model is EventModel em
                && !ShunModEventRegistry.SharedEvents.Contains(em))
            {
                ShunModEventRegistry.Register(em);
            }
        }

        return false; // 跳过原版 Init
    }
}