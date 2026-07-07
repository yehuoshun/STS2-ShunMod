using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using Godot;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Events;

/// <summary>
///     自定义事件注册 — 从 ModelDb 取正规实例注入 AllSharedEvents。
///     事件类型由 Core/ContentRegistry 扫描 [EventPool] 属性收集。
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
            Log.Warn("[STS2_ShunMod] _contentById 字段不可用，回退到原版 Init");
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

        Log.Info($"[STS2_ShunMod] SafeInit: {allTypes.Length} 类型, {created} 新建, {contentById.Count - created} 已存在, {ShunModEventRegistry.EventTypes.Count} 事件类型");

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
///     使用 Dictionary 缓存纹理引用，避免每次创建肖像都查文件系统。
/// </summary>
[HarmonyPatch(typeof(EventModel), "CreateInitialPortrait")]
[HarmonyPriority(Priority.First)]
public static class EventPortraitRedirectPatch
{
    // ═══════════════════════════════════════════════════════════
    //  肖像纹理缓存
    // ═══════════════════════════════════════════════════════════
    //
    //  缓存设计原因：
    //  ResourceLoader.Exists 和 ResourceLoader.Load 都是文件系统 IO 操作。
    //  事件类型在运行时不会变化，每个事件类型最多查一次就知道有没有自定义图片。
    //  Dictionary 缓存后，后续创建肖像直接返回缓存的 Texture2D 或 null，
    //  避免重复 IO。
    //
    //  注意：Dictionary 不是线程安全的，但 EventModel 创建在游戏主线程，
    //  不存在并发访问问题。
    //
    // ═══════════════════════════════════════════════════════════
    private static readonly Dictionary<Type, Texture2D?> CachedPortraits = new();

    [HarmonyPrefix]
    private static bool Prefix(EventModel __instance, ref Texture2D? __result)
    {
        var type = __instance.GetType();

        // 如果缓存命中，直接返回缓存结果
        if (CachedPortraits.TryGetValue(type, out var cached))
        {
            __result = cached;
            return cached != null ? false : true;
        }

        var modPath = ShunModHelper.EventImagePath(type);

        if (ResourceLoader.Exists(modPath))
        {
            var tex = ResourceLoader.Load<Texture2D>(modPath);
            CachedPortraits[type] = tex;
            __result = tex;
            return false;
        }

        // 没有自定义图片，缓存 null 避免重复检查
        CachedPortraits[type] = null;
        return true;
    }
}