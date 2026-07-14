using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using Godot;
using ShunMod.Shun.Helpers;

namespace ShunMod.Shun.Events;

/// <summary>
///     自定义事件注册 — 从 ModelDb 取正规实例注入 AllSharedEvents。
///     事件类型由 Core/ContentRegistry 扫描 [EventPool] 属性收集。
/// </summary>
public static class ShunModEventRegistry
{
    private static readonly HashSet<Type> EventTypesField = [];
    /// <summary>EventModel 子类类型集合（只读，写入通过 AddEventType）。</summary>
    public static IReadOnlySet<Type> EventTypes => EventTypesField;

    private static readonly List<EventModel> SharedEventsField = [];
    /// <summary>共享事件列表（只读，写入通过 Register）。</summary>
    public static IReadOnlyList<EventModel> SharedEvents => SharedEventsField;

    /// <summary>添加事件类型。由 ContentRegistry 扫描 [EventPool] 时调用。</summary>
    public static void AddEventType(Type type) => EventTypesField.Add(type);

    /// <summary>注册事件实例。若非 act 限定事件，加入 SharedEvents。</summary>
    public static void Register(EventModel eventModel)
    {
        if (!SharedEventsField.Contains(eventModel))
            SharedEventsField.Add(eventModel);
    }
}

/// <summary>
///     ModelDb.Init Prefix — 跳过原版 Init，改为 SafeInit 去重构造。
///     原版 Init 遍历 AllAbstractModelSubtypes 会触发 DuplicateModelException。
///     return false 跳过原版 Init，ExecuteEssential 中后续的 ModelIdSerializationCache.Init + InitIds 正常执行。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
[HarmonyPriority(Priority.First)]
internal static class ModelDbInitSafePatch
{
    private static readonly FieldInfo? ContentByIdField =
        typeof(ModelDb).GetField("_contentById",
            BindingFlags.Static | BindingFlags.NonPublic);

    [HarmonyPrefix]
    // ReSharper disable once UnusedMember.Local
    private static bool Prefix()
    {
        // 检查字段是否存在
        if (ContentByIdField == null)
        {
            Log.Warn("[ShunMod_Shun] _contentById 字段不存在，回退到原版 Init");
            return true;
        }

        // 获取字段值，如果为 null 则创建新字典（原版 Init 尚未初始化该字段）
        var rawValue = ContentByIdField.GetValue(null);
        if (rawValue == null)
        {
            Log.Info("[ShunMod_Shun] _contentById 为 null，创建新字典");
            rawValue = new Dictionary<ModelId, AbstractModel>();
            ContentByIdField.SetValue(null, rawValue);
        }

        if (rawValue is not IDictionary<ModelId, AbstractModel> contentById)
        {
            Log.Warn("[ShunMod_Shun] _contentById 类型不匹配，回退到原版 Init");
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

        Log.Info($"[ShunMod_Shun] SafeInit: {allTypes.Length} 类型, {created} 新建, {contentById.Count - created} 已存在, {ShunModEventRegistry.EventTypes.Count} 事件类型");

        // 注册 ShunMod 事件（Register 内部含去重检查）
        // 注意：mod 程序集中的类型不在 AllAbstractModelSubtypes 中，
        // 需要手动创建实例，否则 contentById 里找不到，事件不会出现。
        foreach (var type in ShunModEventRegistry.EventTypes)
        {
            var id = ModelDb.GetId(type);
            if (contentById.TryGetValue(id, out var model) && model is EventModel em)
            {
                ShunModEventRegistry.Register(em);
            }
            else
            {
                // mod 类型不在 AllAbstractModelSubtypes 中，手动创建
                em = (EventModel)Activator.CreateInstance(type)!;
                contentById[id] = em;
                ShunModEventRegistry.Register(em);
                created++;
            }
        }

        return false; // 跳过原版 Init
    }
}

/// <summary>
///     将 ShunModEventRegistry.SharedEvents 注入 ModelDb.AllSharedEvents。
///     兜底创建：如果 ModelDbInitSafePatch 因故未执行（如 _contentById 字段不可用），
///     在 AllSharedEvents 首次被访问时自动创建事件实例并注册。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
internal static class AllSharedEventsInjectPatch
{
    [HarmonyPostfix]
    // ReSharper disable once UnusedMember.Local
    private static IEnumerable<EventModel> Postfix(IEnumerable<EventModel> __result)
    {
        // 兜底：如果 SafeInit 没跑，这里自己创建事件
        if (ShunModEventRegistry.SharedEvents.Count == 0)
        {
            TryRegisterEvents();
        }

        return __result.Concat(ShunModEventRegistry.SharedEvents).ToList();
    }

    private static void TryRegisterEvents()
    {
        foreach (var type in ShunModEventRegistry.EventTypes)
        {
            try
            {
                var em = (EventModel)Activator.CreateInstance(type)!;
                ShunModEventRegistry.Register(em);
                Log.Info($"[ShunMod_Shun] 兜底注册事件: {type.Name}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[ShunMod_Shun] 兜底注册事件 {type.Name} 失败: {ex.Message}");
            }
        }
    }
}

/// <summary>
///     劫持 EventModel.CreateInitialPortrait，将默认图片路径替换为 mod 资源路径
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
    // ReSharper disable once UnusedMember.Local
    private static bool Prefix(EventModel __instance, ref Texture2D? __result)
    {
        var type = __instance.GetType();

        // 如果缓存命中，直接返回缓存结果
        if (CachedPortraits.TryGetValue(type, out var cached))
        {
            __result = cached;
            return cached == null;
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