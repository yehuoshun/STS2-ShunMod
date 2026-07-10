using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ShunMod.Core.Core.Registry;

/// <summary>
///     内容自动注册器 — 扫描 [CardPool] / [RelicPool] / [EventPool] 特性并注册。
///     约定：一个类只能标记一种 Pool 类型，多重标记会被跳过只注册第一个。
/// </summary>
public static class ContentRegistry
{
    /// <summary>事件类型回调 — 由 Shun 项目的 ModEntry 设置。</summary>
    public static Action<System.Type>? OnEventTypeFound { get; set; }
    /// <summary>
    ///     扫描程序集中所有非抽象类，按特性类型分别注册。
    ///     用 else if 链而非 continue 是为了显式表达三者互斥：
    ///     如果哪天有人误写双重属性，第二个会被静默跳过而不是意外执行。
    /// </summary>
    public static void RegisterAll(Assembly assembly)
    {
        var cardCount = 0;
        var relicCount = 0;
        var eventCount = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract) continue;

            var cardAttr = type.GetCustomAttribute<CardPoolAttribute>();
            if (cardAttr != null)
            {
                // 卡牌：直接注册到指定卡池（如 ColorlessCardPool）
                ModHelper.AddModelToPool(cardAttr.PoolType, type);
                cardCount++;
            }
            else
            {
                var relicAttr = type.GetCustomAttribute<RelicPoolAttribute>();
                if (relicAttr != null)
                {
                    // 遗物：直接注册到指定遗物池（如 SharedRelicPool）
                    ModHelper.AddModelToPool(relicAttr.PoolType, type);
                    relicCount++;
                }
                else if (type.GetCustomAttribute<EventPoolAttribute>() != null)
                {
                    // 事件：仅收集类型，真正实例化推迟到 ModelDb.Init SafeInit
                    // （见 ShunModEventRegistry / ModelDbInit_SafePatch）
                    OnEventTypeFound?.Invoke(type);
                    eventCount++;
                }
            }
        }

        Log.Info($"[ShunMod_Core] ContentRegistry: {cardCount} cards, {relicCount} relics, {eventCount} events");
    }
}