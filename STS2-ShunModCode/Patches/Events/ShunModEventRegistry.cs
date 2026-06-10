using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using Godot;

namespace STS2ShunMod.Patches.Events;

/// <summary>
///     自定义事件注册 — 从 ModelDb 取正规实例注入 AllSharedEvents。
///     事件类型由 Core/ContentRegistry 扫描 [ShunEvent] 属性收集。
/// </summary>
public static class ShunModEventRegistry
{
    /// <summary>EventModel 子类类型集合，由 ContentRegistry 和 ModelDbInit_SafePatch 消费。</summary>
    public static readonly HashSet<Type> EventTypes = [];

    /// <summary>共享事件列表（非 act 限定），由 AllSharedEventsPatch 注入。</summary>
    public static readonly List<EventModel> SharedEvents = [];

    /// <summary>注册事件实例。若非 act 限定事件，加入 SharedEvents。</summary>
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
        return __result.Concat(ShunModEventRegistry.SharedEvents).ToList();
    }
}

/// <summary>
///     ModelDb.Init Prefix — 跳过原版 Init，改为 SafeInit 去重构造。
///     原版 Init 遍历 AllAbstractModelSubtypes 会触发 DuplicateModelException。
///     return false 跳过原版 Init，ExecuteEssential 中后续的 ModelIdSerializationCache.Init + InitIds 正常执行。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyPriority(Priority.First)]
internal static class ModelDbInit_SafePatch
{
    private static readonly FieldInfo? ContentByIdField =
        typeof(ModelDb).GetField("_contentById",
            BindingFlags.Static | BindingFlags.NonPublic);

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
            if (contentById.ContainsKey(id)) continue;

            var value = (AbstractModel)Activator.CreateInstance(type)!;
            contentById[id] = value;
            created++;
        }

        Log.Info($"[STS2-ShunMod] SafeInit: {allTypes.Length} 类型, {created} 新建, {contentById.Count - created} 已存在, {ShunModEventRegistry.EventTypes.Count} 事件类型");

        // 注册 ShunMod 事件
        foreach (var type in ShunModEventRegistry.EventTypes)
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

/// <summary>
///     劫持 EventModel.CreateInitialPortrait，将默认图片路径替换为 mod 资源路径。
/// </summary>
[HarmonyPatch(typeof(EventModel), "CreateInitialPortrait")]
[HarmonyPriority(Priority.First)]
public static class EventPortraitRedirectPatch
{
    private const string EventImageRoot = "res://STS2-ShunMod/images/events/shunEvents";

    [HarmonyPrefix]
    private static bool Prefix(EventModel __instance, ref Texture2D? __result)
    {
        var modId = __instance.Id.Entry;
        var modPath = $"{EventImageRoot}/{modId.ToLowerInvariant()}.png";

        if (!ResourceLoader.Exists(modPath)) return true;

        __result = ResourceLoader.Load<Texture2D>(modPath);
        return false;
    }
}