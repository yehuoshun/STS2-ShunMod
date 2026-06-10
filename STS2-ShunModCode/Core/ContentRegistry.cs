using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2ShunMod.Patches.Events;

namespace STS2ShunMod.Core;

/// <summary>
///     内容自动注册器 — 扫描 [CardPool] / [RelicPool] / [EventPool] 属性并注册。
/// </summary>
public static class ContentRegistry
{
    /// <summary>
    ///     扫描程序集中所有非抽象类，按属性类型分别注册。
    /// </summary>
    public static void RegisterAll(Assembly assembly)
    {
        var cardCount = 0;
        var relicCount = 0;
        var eventCount = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract) continue;

            // 卡牌
            var cardAttr = type.GetCustomAttribute<CardPoolAttribute>();
            if (cardAttr != null)
            {
                ModHelper.AddModelToPool(cardAttr.PoolType, type);
                cardCount++;
                continue;
            }

            // 遗物
            var relicAttr = type.GetCustomAttribute<RelicPoolAttribute>();
            if (relicAttr != null)
            {
                ModHelper.AddModelToPool(relicAttr.PoolType, type);
                relicCount++;
                continue;
            }

            // 事件（收集类型，由 ShunModEventRegistry 在 ModelDb.Init 时实例化）
            if (type.GetCustomAttribute<EventPoolAttribute>() != null)
            {
                ShunModEventRegistry.EventTypes.Add(type);
                eventCount++;
            }
        }

        Log.Info($"[STS2-ShunMod] ContentRegistry: {cardCount} cards, {relicCount} relics, {eventCount} events");
    }
}